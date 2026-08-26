using System.ComponentModel.DataAnnotations;

namespace MyERP.Shared;

/// <summary>
/// Like <see cref="EmailAddressAttribute"/>, but also accepts an empty/whitespace string as valid
/// (not just null). The base attribute only special-cases null, so an optional email field bound
/// to an Angular form control — which sends "" rather than null when left blank — always failed
/// validation. Use this in place of a bare [EmailAddress] on any nullable Email property.
/// EmailAddressAttribute is sealed, so this wraps an instance rather than inheriting.
/// </summary>
public class OptionalEmailAddressAttribute : ValidationAttribute
{
    private readonly EmailAddressAttribute _inner = new();

    public override bool IsValid(object? value)
    {
        if (value is string s && string.IsNullOrWhiteSpace(s))
            return true;
        return _inner.IsValid(value);
    }
}
