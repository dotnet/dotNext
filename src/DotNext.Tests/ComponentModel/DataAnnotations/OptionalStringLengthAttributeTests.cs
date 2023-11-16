using System.ComponentModel.DataAnnotations;

namespace DotNext.ComponentModel.DataAnnotations;

public sealed class OptionalStringLengthAttributeTests : Test
{
    public sealed class DataModel
    {
        [OptionalStringLength(100, MinimumLength = 5)]
        [Required<string>]
        public Optional<string> StringProperty { get; set; }
    }

    [Fact]
    public static void ValidateOptionalStringSuccessfully()
    {
        var model = new DataModel
        {
            StringProperty = "Hello, world!"
        };

        var context = new ValidationContext(model);
        var result = new List<ValidationResult>();
        True(Validator.TryValidateObject(model, context, result, validateAllProperties: true));
        Empty(result);
    }

    [Fact]
    public static void ValidateOptionalStringUnsuccessfully()
    {
        var model = new DataModel
        {
            StringProperty = Optional<string>.None
        };

        var context = new ValidationContext(model);
        var result = new List<ValidationResult>();
        False(Validator.TryValidateObject(model, context, result, validateAllProperties: true));
        NotEmpty(result);
    }
}