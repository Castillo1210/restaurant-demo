using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Polly;
using CaraNegra.Application.Common.Interfaces;
using CaraNegra.Domain.Common;

namespace CaraNegra.Infrastructure.Resilience;

public abstract class ResilientRepository<TEntity> where TEntity : BaseEntity
{
    protected readonly IApplicationDbContext _context;
    protected readonly DbSet<TEntity> _dbSet;
    protected readonly IAsyncPolicy _resiliencePolicy;

    protected ResilientRepository(IApplicationDbContext context)
    {
        _context = context;
        _dbSet = ((DbContext)context).Set<TEntity>();
        _resiliencePolicy = DatabaseResiliencePolicy.CreateCombinedPolicy();
    }

    public virtual async Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _resiliencePolicy.ExecuteAsync(async () =>
            await _dbSet.FindAsync(new object[] { id }, cancellationToken));
    }

    public virtual async Task<List<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _resiliencePolicy.ExecuteAsync(async () =>
            await _dbSet.ToListAsync(cancellationToken));
    }

    public virtual async Task<List<TEntity>> GetWhereAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _resiliencePolicy.ExecuteAsync(async () =>
            await _dbSet.Where(predicate).ToListAsync(cancellationToken));
    }

    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return await _resiliencePolicy.ExecuteAsync(async () =>
        {
            _dbSet.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return entity;
        });
    }

    public virtual async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return await _resiliencePolicy.ExecuteAsync(async () =>
        {
            _dbSet.Update(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return entity;
        });
    }

    public virtual async Task<bool> SoftDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _resiliencePolicy.ExecuteAsync(async () =>
        {
            var entity = await _dbSet.FindAsync(new object[] { id }, cancellationToken);
            if (entity == null)
                return false;

            if (entity is ISoftDeletable softDeletable)
            {
                softDeletable.EstaActivo = false;
            }
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        });
    }

    public virtual async Task<bool> HardDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _resiliencePolicy.ExecuteAsync(async () =>
        {
            var entity = await _dbSet.FindAsync(new object[] { id }, cancellationToken);
            if (entity == null)
                return false;

            _dbSet.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        });
    }
}