using FluentValidation;

namespace Phonebook.Application.Contacts;

public sealed class ContactInputValidator : AbstractValidator<ContactInput>
{
    // Optional leading +, then digits with spaces, dashes, dots or brackets.
    private const string PhonePattern = @"^\+?[0-9\s\-().]+$";

    public ContactInputValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).MaximumLength(100);
        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .MaximumLength(32)
            .Matches(PhonePattern).WithMessage("Phone number can only contain digits, spaces, dashes, dots, brackets and a leading +.")
            .Must(phone => phone.Count(char.IsDigit) is >= 6 and <= 15).WithMessage("Phone number must contain 6 to 15 digits.");
        RuleFor(x => x.Email)
            .EmailAddress().MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Address).MaximumLength(300);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
