using FluentValidation;

public class VaultItemValidator : AbstractValidator<VaultItem>
{
    public VaultItemValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(100).Matches(@"^[a-zA-Z0-9\s-_]+$").WithMessage("Title contains invalid characters.");
        RuleFor(x => x.Content).NotEmpty().MaximumLength(500);
    }
}