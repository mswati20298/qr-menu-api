using FluentValidation;

namespace QrMenu.Application.SuperAdmins;

public class SuperAdminLoginRequestValidator : AbstractValidator<SuperAdminLoginRequest>
{
    public SuperAdminLoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
    }
}
