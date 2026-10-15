using System.ComponentModel.DataAnnotations;

namespace Apogeu.DemoCasino.Apogeu;

/// <summary>Address of the Apogeu API and the HMAC key of the demo operator (SPEC-0043 §6, SPEC-0009).</summary>
internal sealed class ApogeuOptions
{
    public const string SectionName = "Apogeu";

    [Required]
    public Uri? BaseUrl { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string OperatorKeyId { get; set; } = string.Empty;

    /// <summary>The HMAC secret, used as the UTF-8 bytes of the string. Never logged nor returned.</summary>
    [Required(AllowEmptyStrings = false)]
    public string OperatorSecret { get; set; } = string.Empty;
}
