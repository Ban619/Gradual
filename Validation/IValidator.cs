namespace Gradual.Validation;

/// <summary>
/// Generic validator interface
/// </summary>
public interface IValidator<T>
{
    ValidationResult Validate(T item);
}
