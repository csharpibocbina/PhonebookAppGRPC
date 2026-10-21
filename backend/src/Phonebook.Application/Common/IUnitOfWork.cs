namespace Phonebook.Application.Common;

/// <summary>Commits all pending repository changes in one transaction.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
