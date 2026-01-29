 using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace MES_WPF.Data.Repositories
{
    //where T : class 限定 T 为引用类型,封装通用 CRUD 操作
    public class Repository<T> : IRepository<T> where T : class
    {
        // 受保护字段：子类（如UserRepository）可直接访问DbContext
        protected readonly MesDbContext _context;
        // 泛型DbSet：对应实体的数据库表（如T=User时，_dbSet=context.Users）
        protected readonly DbSet<T> _dbSet;

        // 构造函数注入DbContext（DI容器自动传入）
        public Repository(MesDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>(); // 动态获取实体对应的DbSet
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        //对比普通委托：Func<T, bool> 会先加载所有数据到内存再过滤，Expression<Func<T, bool>> 会在数据库端过滤（性能更优）。
        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(predicate).ToListAsync();
        }

        //EF Core 高效查询主键（优先查内存缓存，再查数据库）
        public virtual async Task<T?> GetByIdAsync(object id)
        {
            return await _dbSet.FindAsync(id);
        }

        //EF Core 异步添加实体，返回EntityEntry<T>
        public virtual async Task<T> AddAsync(T entity)
        {
            var entry = await _dbSet.AddAsync(entity);
            await SaveChangesAsync();
            return entry.Entity;
        }

        public virtual async Task<T> UpdateAsync(T entity)
        {
            _dbSet.Attach(entity); // 附加实体到DbContext（若实体未被跟踪）
            _context.Entry(entity).State = EntityState.Modified; // 标记为修改状态
            await SaveChangesAsync();
            return entity;
        }

        public virtual async Task DeleteAsync(T entity)
        {
            if (_context.Entry(entity).State == EntityState.Detached)
            {
                _dbSet.Attach(entity);
            }
            
            _dbSet.Remove(entity);
            await SaveChangesAsync();
        }

        public virtual async Task DeleteByIdAsync(object id)
        {
            var entity = await GetByIdAsync(id);
            if (entity != null)
            {
                await DeleteAsync(entity);
            }
        }

        public virtual async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}