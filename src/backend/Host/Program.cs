using Apogeu.Backoffice.Features;
using Apogeu.Betting.Features;
using Apogeu.Fairness.Features;
using Apogeu.Operators.Features;
using Apogeu.Rounds.Features;
using Apogeu.Wallet.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBackofficeModule();
builder.Services.AddBettingModule();
builder.Services.AddFairnessModule();
builder.Services.AddOperatorsModule();
builder.Services.AddRoundsModule();
builder.Services.AddWalletModule();

var app = builder.Build();

app.MapBackofficeEndpoints();
app.MapBettingEndpoints();
app.MapFairnessEndpoints();
app.MapOperatorsEndpoints();
app.MapRoundsEndpoints();
app.MapWalletEndpoints();

app.Run();
