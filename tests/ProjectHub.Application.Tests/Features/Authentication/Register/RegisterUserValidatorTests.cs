using FluentValidation.TestHelper;
using ProjectHub.Application.Features.Authentication.Register;

namespace ProjectHub.Application.Tests.Features.Authentication.Register;

/// <summary>
/// Unit tests for <see cref="RegisterUserValidator"/>. FluentValidation's TestHelper gives us
/// a fluent API for asserting which properties fail which rules — no need to manually inspect
/// the ValidationResult. Each test exercises ONE validation rule in isolation to keep failures
/// obvious and make the suite a living spec of the input contract.
/// </summary>
public sealed class RegisterUserValidatorTests
{
    private readonly RegisterUserValidator _validator = new();

    #region Email Validation

    [Fact]
    public void Validate_WhenEmailIsEmpty_ShouldHaveValidationError()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: string.Empty,
            FirstName: "John",
            LastName: "Doe",
            Password: "SecurePass1");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email is required.");
    }

    [Fact]
    public void Validate_WhenEmailExceedsMaxLength_ShouldHaveValidationError()
    {
        // Arrange: 321 characters (max is 320)
        var longEmail = new string('a', 310) + "@domain.com";
        var command = new RegisterUserCommand(
            Email: longEmail,
            FirstName: "John",
            LastName: "Doe",
            Password: "SecurePass1");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email cannot exceed 320 characters.");
    }

    [Fact]
    public void Validate_WhenEmailFormatIsInvalid_ShouldHaveValidationError()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "not-an-email",
            FirstName: "John",
            LastName: "Doe",
            Password: "SecurePass1");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email format is invalid.");
    }

    [Fact]
    public void Validate_WhenEmailIsValid_ShouldNotHaveValidationError()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "user@example.com",
            FirstName: "John",
            LastName: "Doe",
            Password: "SecurePass1");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    #endregion

    #region FirstName Validation

    [Fact]
    public void Validate_WhenFirstNameIsEmpty_ShouldHaveValidationError()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "user@example.com",
            FirstName: string.Empty,
            LastName: "Doe",
            Password: "SecurePass1");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FirstName)
            .WithErrorMessage("First name is required.");
    }

    [Fact]
    public void Validate_WhenFirstNameExceedsMaxLength_ShouldHaveValidationError()
    {
        // Arrange: 101 characters (max is 100)
        var longName = new string('J', 101);
        var command = new RegisterUserCommand(
            Email: "user@example.com",
            FirstName: longName,
            LastName: "Doe",
            Password: "SecurePass1");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FirstName)
            .WithErrorMessage("First name cannot exceed 100 characters.");
    }

    [Fact]
    public void Validate_WhenFirstNameIsValid_ShouldNotHaveValidationError()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "user@example.com",
            FirstName: "John",
            LastName: "Doe",
            Password: "SecurePass1");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.FirstName);
    }

    #endregion

    #region LastName Validation

    [Fact]
    public void Validate_WhenLastNameIsEmpty_ShouldHaveValidationError()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "user@example.com",
            FirstName: "John",
            LastName: string.Empty,
            Password: "SecurePass1");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LastName)
            .WithErrorMessage("Last name is required.");
    }

    [Fact]
    public void Validate_WhenLastNameExceedsMaxLength_ShouldHaveValidationError()
    {
        // Arrange: 101 characters (max is 100)
        var longName = new string('D', 101);
        var command = new RegisterUserCommand(
            Email: "user@example.com",
            FirstName: "John",
            LastName: longName,
            Password: "SecurePass1");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LastName)
            .WithErrorMessage("Last name cannot exceed 100 characters.");
    }

    [Fact]
    public void Validate_WhenLastNameIsValid_ShouldNotHaveValidationError()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "user@example.com",
            FirstName: "John",
            LastName: "Doe",
            Password: "SecurePass1");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.LastName);
    }

    #endregion

    #region Password Validation

    [Fact]
    public void Validate_WhenPasswordIsEmpty_ShouldHaveValidationError()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "user@example.com",
            FirstName: "John",
            LastName: "Doe",
            Password: string.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password is required.");
    }

    [Fact]
    public void Validate_WhenPasswordIsTooShort_ShouldHaveValidationError()
    {
        // Arrange: 7 characters (min is 8)
        var command = new RegisterUserCommand(
            Email: "user@example.com",
            FirstName: "John",
            LastName: "Doe",
            Password: "Short1A");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must be at least 8 characters.");
    }

    [Fact]
    public void Validate_WhenPasswordExceedsMaxLength_ShouldHaveValidationError()
    {
        // Arrange: 129 characters (max is 128)
        var longPassword = "A1" + new string('a', 127);
        var command = new RegisterUserCommand(
            Email: "user@example.com",
            FirstName: "John",
            LastName: "Doe",
            Password: longPassword);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password cannot exceed 128 characters.");
    }

    [Fact]
    public void Validate_WhenPasswordLacksUppercase_ShouldHaveValidationError()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "user@example.com",
            FirstName: "John",
            LastName: "Doe",
            Password: "lowercase1");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one uppercase letter.");
    }

    [Fact]
    public void Validate_WhenPasswordLacksLowercase_ShouldHaveValidationError()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "user@example.com",
            FirstName: "John",
            LastName: "Doe",
            Password: "UPPERCASE1");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one lowercase letter.");
    }

    [Fact]
    public void Validate_WhenPasswordLacksDigit_ShouldHaveValidationError()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "user@example.com",
            FirstName: "John",
            LastName: "Doe",
            Password: "NoDigitsHere");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one digit.");
    }

    [Fact]
    public void Validate_WhenPasswordIsValid_ShouldNotHaveValidationError()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "user@example.com",
            FirstName: "John",
            LastName: "Doe",
            Password: "SecurePass1");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    #endregion

    [Fact]
    public void Validate_WhenAllFieldsAreValid_ShouldNotHaveAnyValidationErrors()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "user@example.com",
            FirstName: "John",
            LastName: "Doe",
            Password: "SecurePass1");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
