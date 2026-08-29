#if false
using System.Data;
using System.Diagnostics.CodeAnalysis;


using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Ngan.Dict.Core.Infra;
using Ngan.Dict.Core.Infra.Db;
using Ngan.Dict.Core.Model;
using Ngan.Dict.Core.Model.Po;
using Tsinswreng.CsSql;

namespace Ngan.Dict.Backend.Db;
//using T = Bo_Word;

using DbCtx = DbContext;

public  partial class RepoEf
<
[DynamicallyAccessedMembers(
	DynamicallyAccessedMemberTypes.PublicConstructors |
	DynamicallyAccessedMemberTypes.NonPublicConstructors |
	DynamicallyAccessedMemberTypes.PublicFields |
	DynamicallyAccessedMemberTypes.NonPublicFields |
	DynamicallyAccessedMemberTypes.PublicProperties |
	DynamicallyAccessedMemberTypes.NonPublicProperties |
	DynamicallyAccessedMemberTypes.Interfaces
)]
	TEntity
	,TId
>
	where TEntity: class, I_Id<TId>
	where TId : IEquatable<TId>
{

	public RepoEf(DbCtx dbCtx){
		this.DbCtx = dbCtx;
	}

	public DbCtx DbCtx{get;set;}

	public async Task<Func<
		IEnumerable<TEntity>
		,CancellationToken
		,Task<nil>
	>> FnInsertManyAsy(
		IDbFnCtx DbFnCtx
		,CancellationToken ct
	){
		var Fn = async(
			IEnumerable<TEntity> Entitys
			,CancellationToken ct
		)=>{
			await DbCtx.Set<TEntity>().AddRangeAsync(Entitys, ct);
			await DbCtx.SaveChangesAsync(ct);
			return NIL;
		};
		return Fn;
	}

	public async Task<Func<
		TId
		,CancellationToken
		,Task<TEntity?>
	>> FnSelectByIdAsy(
		IDbFnCtx DbFnCtx
		,CancellationToken ct
	){
		var Fn = async(
			TId Id
			,CancellationToken ct
		)=>{
			if(Id is not TId id){
				throw new FatalLogicErr("Id is not T_Id id");
			}
			var entity = await DbCtx.Set<TEntity>().AsNoTracking().Select(x=>x)
				//.Where(x=>x.Id==id) // 运算符“==”无法应用于“T_Id”和“T_Id”类型的操作数CS0019
				.Where(x=>x.Id.Equals(id))
				.FirstOrDefaultAsync(ct)
			;
			return entity;
		};
		return Fn;
	}

	public async Task<Func<
		IEnumerable<Id_Dict<T_Id2>>
		,CancellationToken
		,Task<nil>
	>> FnUpdateManyAsy<T_Id2>(
		IDbFnCtx DbFnCtx
		,CancellationToken ct
	){
		var Fn = async(
			IEnumerable<Id_Dict<T_Id2>> Id_Dicts
			,CancellationToken ct
		)=>{
			var DbSet = DbCtx.Set<TEntity>();
			foreach(var id_dict in Id_Dicts){
				var id = id_dict.Id;
				var dict = id_dict.Dict;
				var Old = await DbSet.Select(x=>x).Where(x=>x.Id.Equals(id)).FirstOrDefaultAsync(ct);
				if(Old == null){
					continue;
				}
				CoreDictMapper.Inst.AssignT(Old, dict);//源生成器實現、無反射
				if(Old is IPoBase po){
					po.UpdatedAt = DateTimeOffset.Now.ToUnixTimeMilliseconds();
				}
			}//~for
			await DbCtx.SaveChangesAsync();
			return NIL;
		};
		return Fn;
	}

	// public async Task<I_Answer<nil>> UpdateManyAsy(IEnumerable<T_Entity> EntityList){
	// 	I_Answer<nil> ans = new Answer<nil>();
	// 	IDbContextTransaction tx = null!;
	// 	//DbCtx.Entry
	// 	try{
	// 		tx = await DbCtx.Database.BeginTransactionAsync();

	// 		foreach (var newEntity in EntityList){
	// 			var existingEntity = DbCtx.Set<T_Entity>().Find(newEntity.Id);
	// 			if (existingEntity == null){
	// 				break;
	// 			}
	// 			foreach (var property in DbCtx.Entry(existingEntity).Properties){
	// 				var newValue = newEntity.GetType().GetProperty(property.Metadata.Name)?.GetValue(newEntity);
	// 				if (!Equals(property.CurrentValue, newValue)){
	// 					property.CurrentValue = newValue;
	// 					property.IsModified = true;
	// 				}
	// 			}
	// 		}

	// 		await DbCtx.SaveChangesAsync();
	// 		await tx.CommitAsync();
	// 		return ans.OkWith(Nil);
	// 	}
	// 	catch (Exception e){
	// 		ans.AddErrException(e);
	// 		await tx.RollbackAsync();
	// 	}
	// 	return ans;
	// }

	public async Task<Func<
		IEnumerable<object>
		,CancellationToken
		,Task<nil>
	>> FnDeleteManyByIdAsy(
		IDbFnCtx DbFnCtx
		,CancellationToken ct
	){
		var Fn = async(
			IEnumerable<object> IdList
			,CancellationToken ct
		)=>{
			await DbCtx.Set<TEntity>()
				.Where(e=>IdList.Contains(e.Id))
				.ExecuteDeleteAsync(ct)
			;//仅生成一条 DELETE FROM ... WHERE Id IN (1, 2, 3) SQL 语句、Contains(e.Id)並非在內存中查找
			return NIL;
		};
		return Fn;
	}


	public async Task<T_Ret> RunInTxnAsy<T_Ret>(
		Func<CancellationToken, Task<T_Ret>> FnAsy
		,CancellationToken ct
	){
		using var Tx = await DbCtx.Database.BeginTransactionAsync(ct);
		try{
			var ans = await FnAsy(ct);
			await Tx.CommitAsync(ct);
			return ans;
		}
		catch (System.Exception){
			await Tx.RollbackAsync(ct);
			throw;
		}
	}

}

#endif
