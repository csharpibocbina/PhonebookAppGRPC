using Phonebook.Application.Contacts;
using Phonebook.UnitTests.TestHelpers;

namespace Phonebook.UnitTests.Contacts;

public class ContactInputValidatorTests
{
    private readonly ContactInputValidator _validator = new();

    [Fact]
    public void Valid_input_passes() => _validator.Validate(TestData.ValidInput()).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData("+63 917 555 0101")]
    [InlineData("09175550101")]
    [InlineData("(02) 8555-0101")]
    [InlineData("+1.415.555.0104")]
    public void Accepts_common_phone_formats(string phone) =>
        _validator.Validate(TestData.ValidInput() with { PhoneNumber = phone }).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("12345")]
    [InlineData("+1 234 567 890 123 456 7")]
    [InlineData("+63 917 555 0101 ext 5")]
    public void Rejects_bad_phone_numbers(string phone)
    {
        var result = _validator.Validate(TestData.ValidInput() with { PhoneNumber = phone });

        result.Errors.ShouldContain(e => e.PropertyName == nameof(ContactInput.PhoneNumber));
    }

    [Fact]
    public void First_name_is_required()
    {
        var result = _validator.Validate(TestData.ValidInput() with { FirstName = " " });

        result.Errors.ShouldContain(e => e.PropertyName == nameof(ContactInput.FirstName));
    }

    [Fact]
    public void Email_is_optional_but_must_be_valid_when_given()
    {
        _validator.Validate(TestData.ValidInput() with { Email = null }).IsValid.ShouldBeTrue();
        _validator.Validate(TestData.ValidInput() with { Email = "not-an-email" }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Notes_are_limited_to_1000_characters() =>
        _validator.Validate(TestData.ValidInput() with { Notes = new string('x', 1001) }).IsValid.ShouldBeFalse();
}
