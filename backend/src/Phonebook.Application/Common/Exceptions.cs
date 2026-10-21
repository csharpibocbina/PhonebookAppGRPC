namespace Phonebook.Application.Common;

// Plain C# exceptions. The application layer knows nothing about gRPC.
// The API's ExceptionInterceptor translates them into gRPC status codes:
//   NotFoundException             -> NOT_FOUND
//   ConflictException             -> ALREADY_EXISTS
//   AuthenticationFailedException -> UNAUTHENTICATED
//   FluentValidation.ValidationException -> INVALID_ARGUMENT

public class NotFoundException(string message) : Exception(message);

public class ConflictException(string message) : Exception(message);

public class AuthenticationFailedException(string message) : Exception(message);
