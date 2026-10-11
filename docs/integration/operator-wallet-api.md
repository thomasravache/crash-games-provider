# API de carteira do operador (Apogeu) — v1

> Contrato público da **carteira transparente** (seamless wallet) que o cassino implementa para jogar os crash games
> Apogeu com saldo real. Origem: SPEC-0021 §6 (contrato v4), ADR-0007 (carteira transparente + livro-razão), assinatura
> da SPEC-0009 (ADR-0015). O adaptador HTTP do lado Apogeu ainda não está publicado; o modo demo já usa a mesma porta.

## 1. Modelo

- **O saldo do jogador fica no operador.** O Apogeu chama a sua carteira para debitar a aposta, creditar o prêmio e
  estornar um débito; o Apogeu nunca guarda o saldo real.
- **Toda operação é idempotente pelo `transactionId`.** Rede falha, timeouts acontecem e o Apogeu **reenvia a mesma
  operação com o mesmo `transactionId`** até ter uma resposta definitiva. A sua carteira deve aplicar cada
  `transactionId` **uma única vez** e, numa repetição, responder `DUPLICATE` com o resultado original.
- **Livro-razão do lado Apogeu.** Cada operação confirmada vira um lançamento imutável de partidas dobradas no Apogeu
  (espelho das suas transações), base da auditoria e da conciliação.
- **Aposta só vale com débito confirmado.** Se o débito não tiver resposta definitiva, o Apogeu não aceita a aposta e
  envia o estorno (`rollback`) desse débito, repetindo até a confirmação.
- **Prêmio nunca é descartado.** Créditos sem resposta definitiva são reenviados (mesmo `transactionId`) até a confirmação.

## 2. Endpoints

O operador informa a `walletBaseUrl` (HTTPS) no cadastro. Todas as chamadas são `POST` com corpo JSON (UTF-8,
`Content-Type: application/json`):

| Operação | Caminho | Quando |
|---|---|---|
| Saldo | `POST {walletBaseUrl}/apogeu/v1/balance` | ao abrir o jogo e para exibir o saldo |
| Débito | `POST {walletBaseUrl}/apogeu/v1/debit` | ao aceitar uma aposta |
| Crédito | `POST {walletBaseUrl}/apogeu/v1/credit` | ao pagar um prêmio (cash-out ou liquidação) |
| Estorno | `POST {walletBaseUrl}/apogeu/v1/rollback` | para desfazer um débito (aposta não aceita, rodada anulada, débito incerto) |

## 3. Assinatura (HMAC)

Cada requisição do Apogeu é assinada com o segredo da chave de API combinada com o operador (mesmo esquema da
integração operador → Apogeu, SPEC-0009):

| Cabeçalho | Valor |
|---|---|
| `X-Apogeu-Key` | identificador da chave que assinou |
| `X-Apogeu-Timestamp` | Unix time em segundos do momento da assinatura |
| `X-Apogeu-Signature` | hex minúsculo de `HMAC-SHA256(segredo, timestamp + "\n" + MÉTODO + "\n" + caminho + "\n" + hex(SHA256(corpo)))` |

- `MÉTODO` em maiúsculas (`POST`); `caminho` é o caminho da URL como enviado (por exemplo `/apogeu/v1/debit`); o hash é
  dos bytes exatos do corpo.
- **Verifique** a assinatura em tempo constante e recuse timestamps fora de **±300 s** do seu relógio; responda `401`
  sem processar nada quando a verificação falhar.
- O segredo nunca trafega nem aparece em logs (ADR-0014).

## 4. Requisição

```json
{
  "transactionId": "bet:3f2b8c1e-5d4a-4f7e-9c2b-1a6e8d9f0b3c:debit",
  "playerId": "p-8841",
  "currency": "BRL",
  "amount": "10.00",
  "roundId": "0b9e6f3a-2c1d-4e5f-8a7b-6c5d4e3f2a1b",
  "betId": "3f2b8c1e-5d4a-4f7e-9c2b-1a6e8d9f0b3c"
}
```

| Campo | Tipo | Regras |
|---|---|---|
| `transactionId` | string (≤ 128) | Chave de idempotência, determinística por operação da aposta: `bet:{betId}:debit`, `bet:{betId}:credit`, `bet:{betId}:rollback` (`betId` no formato GUID `D`). |
| `playerId` | string | ID pseudônimo do jogador que **você** enviou no lançamento; nenhum dado pessoal (ADR-0014). |
| `currency` | string | Código ISO 4217 (por exemplo `BRL`). |
| `amount` | string decimal | Valor positivo com duas casas, como string (`"10.00"`), nunca número de ponto flutuante. Ausente em `balance` e em `rollback` (o estorno devolve exatamente o valor do débito original). |
| `roundId` | string (GUID) ou `null` | Rodada da aposta. **`roundId` nulo no débito de aposta para a próxima rodada e no estorno desse débito** (aposta enfileirada, cuja rodada ainda não existe). O crédito sempre leva a rodada. Ausente em `balance`. |
| `betId` | string (GUID) | Aposta a que a operação pertence. Ausente em `balance`. |
| `originalTransactionId` | string | Só em `rollback`: o `transactionId` do débito a estornar (`bet:{betId}:debit`). |

`balance` leva apenas `playerId` e `currency`.

## 5. Resposta

Sempre `200` com JSON quando a operação teve resultado definitivo (inclusive recusas de negócio):

```json
{
  "status": "OK",
  "balance": "2990.00",
  "currency": "BRL",
  "transactionId": "bet:3f2b8c1e-5d4a-4f7e-9c2b-1a6e8d9f0b3c:debit"
}
```

| `status` | Significado | O Apogeu faz |
|---|---|---|
| `OK` | Operação aplicada agora; `balance` é o saldo logo depois dela. | Confirma (aceita a aposta, registra o prêmio ou o estorno). |
| `INSUFFICIENT_FUNDS` | Débito recusado: o saldo não cobre `amount`; nada foi debitado. `balance` é o saldo atual. | Recusa a aposta; não reenvia. |
| `DUPLICATE` | O `transactionId` já foi processado antes; nada novo foi aplicado. `balance` é o saldo **logo depois da operação original**. | Trata como o resultado original (`OK`). |
| `PLAYER_NOT_FOUND` | O `playerId` não existe na sua carteira. | Recusa a operação; não reenvia. |
| `TRANSACTION_NOT_FOUND` | Só em `rollback`: o débito `originalTransactionId` não existe; nada foi lançado. | Considera o estorno encerrado (não há o que devolver); não reenvia. |

- `currency` e `transactionId` repetem os da requisição; `balance` é string decimal com duas casas.
- **Débito atrasado depois do estorno:** se um `rollback` chegar antes do débito que ele estorna, responda
  `TRANSACTION_NOT_FOUND`. A corrida "débito que chega depois do seu estorno" é tratada pela sua carteira (o débito
  atrasado não pode ficar aplicado sem estorno).
- Um `rollback` repetido responde `DUPLICATE`, nunca devolve o valor duas vezes; um débito estornado continua estornado.

## 6. Erros e novas tentativas

Qualquer resposta que não seja `200` com um `status` da tabela acima (erro HTTP, timeout, conexão perdida) é um
resultado **incerto**:

| Operação incerta | O Apogeu faz |
|---|---|
| Débito | Não aceita a aposta e envia o `rollback` desse débito até a confirmação. |
| Crédito | Reenvia com o **mesmo** `transactionId` até a confirmação. |
| Estorno | Reenvia com o **mesmo** `transactionId` até a confirmação. |

Regras para a sua implementação:

1. Grave o `transactionId` e o resultado **na mesma transação** que altera o saldo (restrição única no `transactionId`).
2. Duas requisições simultâneas com o mesmo `transactionId` produzem um único efeito; a segunda recebe `DUPLICATE`.
3. Dois débitos **diferentes** simultâneos do mesmo jogador nunca deixam o saldo negativo: serialize por jogador.

## 7. Modo demo

Operadores em modo demo não implementam esta API: o Apogeu usa o adaptador **demo**, com R$ 3.000,00 fictícios por
jogador e operador, persistentes entre sessões, e recarga para R$ 3.000,00 quando o saldo fica abaixo da aposta mínima
da moeda. A sequência de operações (débito, crédito, estorno, idempotência) é a mesma descrita aqui.
