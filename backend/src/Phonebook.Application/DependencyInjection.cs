using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Phonebook.Application.Auth;
using Phonebook.Application.Contacts;

namespace Phonebook.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssemblyContaining<ContactInputValidator>(includeInternalTypes: true);
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
