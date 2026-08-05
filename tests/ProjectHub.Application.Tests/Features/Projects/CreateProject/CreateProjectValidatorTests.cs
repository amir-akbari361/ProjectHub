using FluentAssertions;
using FluentValidation.TestHelper;
using ProjectHub.Application.Features.Projects.CreateProject;

namespace ProjectHub.Application.Tests.Features.Projects.CreateProject;

/// <summary>
/// Tests for <see cref="CreateProjectValidator"/>. FluentValidation's TestHelper provides
/// .TestValidate() and .ShouldHaveValidationErrorFor(), which make these tests extremely
/// readable. We assert both the failure cases (empty/too-long fields) and the pass cases
/// (valid lengths, null description) to confirm the rules aren't overly strict.
/// </summary>
public sealed class CreateProjectValidatorTests
{
    private readonly CreateProjectValidator _validator = new();

    [Fact]
    public void Validate_WhenNameIsEmpty_ShouldHaveValidationError()
    {
        // Arrange
        var command = new CreateProjectCommand("", "Valid description");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Name)
            .WithErrorMessage("'Name' must not be empty.");
    }

    [Fact]
    public void Validate_WhenNameExceedsMaxLength_ShouldHaveValidationError()
    {
        // Arrange: 201 characters (one over the 200 limit)
        var longName = new string('A', 201);
        var command = new CreateProjectCommand(longName, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void Validate_WhenDescriptionExceedsMaxLength_ShouldHaveValidationError()
    {
        // Arrange: 2001 characters (one over the 2000 limit)
        var longDescription = new string('X', 2001);
        var command = new CreateProjectCommand("Valid Name", longDescription);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Fact]
    public void Validate_WhenNameIsValid_ShouldNotHaveValidationError()
    {
        // Arrange
        var command = new CreateProjectCommand("Valid Project Name", "A reasonable description");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void Validate_WhenDescriptionIsNull_ShouldNotHaveValidationError()
    {
        // Arrange: description is optional, so null should be valid.
        var command = new CreateProjectCommand("Valid Name", null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(c => c.Description);
    }

    [Fact]
    public void Validate_WhenBothFieldsAreValid_ShouldPassValidation()
    {
        // Arrange
        var command = new CreateProjectCommand(
            "Enterprise Project Management System",
            "A comprehensive Jira-like project tracking tool");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
