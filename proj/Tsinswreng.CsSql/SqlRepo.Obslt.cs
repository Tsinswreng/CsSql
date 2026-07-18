//此文件中的API已廢棄
namespace Tsinswreng.CsSql;

using System.Data;
using Tsinswreng.CsCore;
using Tsinswreng.CsTools;
using Tsinswreng.CsPage;
using Tsinswreng.Srefl;
using System.Collections;
using System.Diagnostics;
using Str_Any = System.Collections.Generic.Dictionary<str, obj?>;
using IStr_Any = System.Collections.Generic.IDictionary<str, obj?>;


public partial class SqlRepo<
	TEntity, TId
>{
	public async Task<Func<
		CT
		,Task<u64>
	>> FnCount(
		IDbFnCtx Ctx
		,CT Ct
	){
		var NCnt = "Cnt";
		var Sql =
$"SELECT COUNT(*) AS {T.Qt(NCnt)} FROM {T.Qt(T.DbTblName)}";

		var Cmd = await Ctx.PrepareToDispose(SqlCmdMkr, Sql, Ct);
		return async(CT Ct)=>{
			var CountDict = await Cmd.AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			u64 R = 0;
			if (CountDict != null){
				if(CountDict.TryGetValue(NCnt, out var Cnt)){
					if(Cnt is u64 || Cnt is i64 || Cnt is u32 || Cnt is i32){
						R = Convert.ToUInt64(Cnt);
					}
				}
			}
			return R;
		};

	}

	public async Task<Func<
		IPageQry
		,CT, Task<IPageAsyE<IDictionary<str, obj?>>>
	>> FnPageAllDict(IDbFnCtx Ctx, CT Ct){
		var T = TblMgr.GetTbl<TEntity>();
		var Sql = $"""
		SELECT * FROM {T.Qt(T.DbTblName)}
		{T.SqlMkr.ParamLimOfst(out var Lim, out var Ofst)}
		""";
		var CountAll = await FnCount(Ctx, Ct);
		var Cmd = await Ctx.PrepareToDispose(SqlCmdMkr, Sql, Ct);
		var Fn = async(IPageQry Qry, CT Ct)=>{
			var Arg = ArgDict.Mk().AddPageQry(Qry, Lim, Ofst);
			var Ran = Cmd.Args(Arg).AsyE1d(Ct);

			u64 Cnt = 0;
			var HasCnt = false;
			if(Qry.WantTotCnt){
				Cnt = await CountAll(Ct);
				HasCnt = true;
			}

			var R = PageAsyE.Mk(
				Qry, Ran, HasCnt, Cnt
			);
			return R;
		};
		return Fn;
	}

	public async Task<Func<
		IPageQry
		,CT, Task<IPageAsyE<TEntity>>
	>> FnPageAll(IDbFnCtx Ctx, CT Ct){
		var T = TblMgr.GetTbl<TEntity>();
		var PageAllDict = await FnPageAllDict(Ctx, Ct);
		var Fn = async(IPageQry Qry, CT Ct)=>{
			var AllDictPage = await PageAllDict(Qry, Ct);
			if(AllDictPage.DataAsyE != null){
				var EntityData = AllDictPage.DataAsyE.Select(d=>T.DbDictToEntity<TEntity>(d));
				var R = PageAsyE.Mk(Qry, EntityData, AllDictPage.HasTotCnt, AllDictPage.TotCnt);
				return R;
			}else{
				return PageAsyE.Mk<TEntity>(Qry, null, false, 0);
			}
		};
		return Fn;
	}

	//public class _ClsInsrtMany<E,I>(SqlRepo<E,I> z)

	/// 適用sqlite
	protected async Task<Func<
		IEnumerable<TEntity>
		,CT
		,Task<nil>
	>> _FnInsertManyLoop(
		IDbFnCtx Ctx
		,bool Prepare
		,CT Ct
	){
		var T = TblMgr.GetTbl<TEntity>();
		var Clause = T.InsertClause(T.Columns.Keys);
		var Sql =
$"INSERT INTO {T.Qt(T.DbTblName)} {Clause}";
		var Cmd = await SqlCmdMkr.MkCmd(Ctx, Sql, Ct);
		if(Prepare){
			Cmd = await SqlCmdMkr.Prepare(Cmd, Ct);
		}
		Ctx?.AddToDispose(Cmd);
		return async(
			IEnumerable<TEntity> Entitys
			,CT ct
		)=>{
			foreach(var (i,entity) in Entitys.Index()){
				var CodeDict = T.EntityToCodeDict(entity);
				var DbDict = T.ToDbDict(CodeDict);
				await Cmd.RawArgs(DbDict).AsyE1d(ct).FirstOrDefaultAsync(ct);
			}
			return NIL;
		};
	}

	public Task<Func<
		IAsyncEnumerable<TEntity>
		,CT
		,Task<nil>
	>> FnInsertAsyE(
		IDbFnCtx Ctx
		,CT Ct
	){
		throw new NotImplementedException();
	}


	public class CfgInsertMany{
		public bool Prepare = true;
		/// 餘ʹ數據量ˋ逾此則用大批插入
		public u64 FullBatchSize = 500;//Sqlite最多支持999個參數
		/// 餘ʹ數據量ˋ不足FullBatchSize則試小批插入
		public u64 SmallBatchSize = 20;
		//public u64 BatchSize = 4;
	}

//TODO 500一批、可用緩衝區、不足一批者 則手動建ʹsql 一次發送。參數可用 Ulid 64進制
	protected async Task<Func<
		IEnumerable<TEntity>,CT,Task<nil>
	>> _FnInsertMany(
		IDbFnCtx Ctx,CfgInsertMany? Cfg, CT Ct
	){
		Cfg??=new();

		if(T.TblMgr?.DbSrcType == EDbSrcType.Sqlite){
			var old = await _FnInsertManyLoop(Ctx, Cfg.Prepare, Ct);
			return old;
		}

		var mkCmd = async(u64 BatchSize)=>{
			var Clause = T.InsertManyClause(T.Columns.Keys, BatchSize);
			var Sql =
	$"INSERT INTO {T.Qt(T.DbTblName)} {Clause}"; //TODO INSERT INTO 多值插入 不被oracle支持
			var Cmd = await SqlCmdMkr.MkCmd(Ctx, Sql, Ct);
			if(Cfg.Prepare){
				Cmd = await SqlCmdMkr.Prepare(Cmd, Ct);
			}
			Ctx?.AddToDispose(Cmd);
			return Cmd;
		};

		u64 SmallSize = Cfg.SmallBatchSize;
		var CmdFullSize = await mkCmd(Cfg.FullBatchSize);//大批 插入 sql命令
		var CmdSmallSize = await mkCmd(SmallSize); // 小批插入sql命令
		var CmdOne = await mkCmd(1);// 插入單條實體

		var Cmd = CmdFullSize;

		return async(IEnumerable<TEntity> Entitys,CT Ct)=>{
			//插入一批
			var OneBatch = async(ISqlCmd Cmd, IList<IDictionary<string, object?>> ArgDicts)=>{
				var FullArgDict = new Dictionary<str, obj?>();
				u64 i = 0;
				foreach(var ArgDict in ArgDicts){
					foreach(var (k,v) in ArgDict){
						FullArgDict[T.NumFieldParam(k,(u64)i).Name] = v;
					}
					i++;
				}
				await Cmd.RawArgs(FullArgDict).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			};

			await using var BatchList = new BatchCollector<IDictionary<str, obj?>,nil>(
				async(ArgDicts, Ct)=>{
					if((u64)ArgDicts.Count >= Cfg.FullBatchSize){
						await OneBatch(Cmd, ArgDicts);
					}else{
						var (SmallBatchCnt, Remainder) = DivideWithRemainder(
							(u64)ArgDicts.Count, SmallSize
						);
						var Reversed = ArgDicts.Reverse().ToList();
						for(u64 i = 0; i < SmallBatchCnt; i++){
							await OneBatch(
								CmdSmallSize
								,PopMany(Reversed, SmallSize)
							);
						}
						for(u64 i = 0; i < Remainder; i++){
							//var sw = Stopwatch.StartNew();
							await OneBatch(
								CmdOne, PopMany(Reversed, 1)
							);
						}
					}

					return NIL;
				}
				,Cfg.FullBatchSize
			);

			foreach(var (i,entity) in Entitys.Index()){
				var CodeDict = T.EntityToCodeDict(entity);
				var DbDict = T.ToDbDict(CodeDict);
				await BatchList.Add(DbDict, Ct);
			}
			await BatchList.End(Ct);
			return NIL;
		};
	}
	static IList<T> PopMany<T>(IList<T> list, u64 cnt) {
		var R = new List<T>();
		for(u64 i = 0; i < cnt; i++){
			var item = list[list.Count - 1];
			list.RemoveAt(list.Count - 1);
			R.Add(item);
		}
		return R;
	}
	public static (u64 quotient, u64 remainder) DivideWithRemainder(
		u64 dividend, u64 divisor
	){
		if (divisor == 0)
			throw new ArgumentException("除數不可為 0", nameof(divisor));

		u64 quotient = dividend / divisor;
		u64 remainder = dividend % divisor;

		return (quotient, remainder);
	}

	[Impl]
	public async Task<Func<
		IEnumerable<TEntity>
		,CT
		,Task<nil>
	>> FnInsertMany(
		IDbFnCtx Ctx
		,CT Ct
	){
		return await _FnInsertMany(Ctx, null, Ct);
	}

	public async Task<Func<
		TEntity
		,CT
		,Task<nil>
	>> FnInsertOne(
		IDbFnCtx Ctx
		,CT Ct
	){
		var InsrtMany = await FnInsertMany(Ctx, Ct);
		return async(Entity, Ct)=>{
			await InsrtMany([Entity], Ct);
			return NIL;
		};
	}


/// 不預編譯。適用于況芝 在事務中 初建表後即添數
	public async Task<Func<
		IEnumerable<TEntity>
		,CT
		,Task<nil>
	>> FnInsertManyNoPrepare(
		IDbFnCtx Ctx
		,CT Ct
	){
		return await _FnInsertMany(Ctx, new CfgInsertMany{Prepare = false}, Ct);
	}


	[Impl]
	public async Task<Func<
		TId
		,CT
		,Task<TEntity?>
	>> FnSlctOneById(
		IDbFnCtx Ctx
		,CT Ct
	){
		var Params = T.Prm(0,0);
		var Sql = $"SELECT * FROM {T.Qt(T.DbTblName)} WHERE {T.QtCol(T.CodeIdName)} = {Params[0]}" ;
		var Cmd = await Ctx.PrepareToDispose(SqlCmdMkr, Sql, Ct);
		return async(Id ,Ct)=>{
			var IdCol = T.Columns[T.CodeIdName];
			var ConvertedId = IdCol.UpperToRaw?.Invoke(Id)??Id;
			var RawDict = await Cmd
				.Args([ConvertedId])
				.AsyE1d(Ct).FirstOrDefaultAsync(Ct)
			;
			if(RawDict == null){
				return null;
			}
			var CodeDict = T.ToCodeDict(RawDict);
			var R = new TEntity();
			T.AssignEntityByCodeDict(typeof(TEntity), R, CodeDict);
			return R;
		};
	}
	
[Impl]
	public async Task<Func<
		IEnumerable<TId>
		,CT
		,Task<IList<TEntity?>>
	>> FnSlctListByIds(//TODO 改潙真批量
		IDbFnCtx Ctx
		,CT Ct
	){
		var SlctOneById = await FnSlctOneById(Ctx, Ct);
		return async(Ids, Ct)=>{
			var R = new List<TEntity?>();
			foreach(var Id in Ids){
				var R1 = await SlctOneById(Id, Ct);
				R.Add(R1);
			}
			return R;
		};
		// async IAsyncEnumerable<TEntity?> Fn(IEnumerable<TId>Ids, CT Ct){
		// 	var R = new List<TEntity?>();
		// 	foreach(var Id in Ids){
		// 		var R1 = await SlctOneById(Id, Ct);
		// 		yield return R1;
		// 	}
		// };
	}

	/// <param name="UpperFieldsToUpdate">潙null旹更新全部字段</param>
	public async Task<Func<
		TId
		,TEntity
		,CT, Task<nil>
	>> FnUpdByIdOld(
		IDbFnCtx Ctx
		,IEnumerable<str>? UpperFieldsToUpdate
		,CT Ct
	){
		var NId = T.CodeIdName;
		UpperFieldsToUpdate = UpperFieldsToUpdate??T.Columns.Keys;
		var Clause = T.UpdateClause(UpperFieldsToUpdate);
		var FieldsToUpdateMap = UpperFieldsToUpdate.ToHashSet();
		var Sql =
$"UPDATE {T.Qt(T.DbTblName)} SET {Clause} WHERE {T.QtCol(NId)} = {T.Prm(NId)}";
		var Cmd = await Ctx.PrepareToDispose(SqlCmdMkr, Sql, Ct);
		return async(Id, Entity, Ct)=>{
			var Arg = ArgDict.Mk(T);
			var CodeDict = T.EntityToCodeDict(Entity);
			foreach(var (k,v) in CodeDict){
				if(FieldsToUpdateMap.Contains(k)){
					Arg.AddT(T.Prm(k), v, k);
				}
			}
			await Cmd.Args(Arg).All1d(Ct);
			return NIL;
		};
	}


	/// <param name="UpperFieldsToUpdate">潙null旹更新全部字段</param>
	public async Task<Func<
		TEntity
		,CT, Task<nil>
	>> FnUpdOneById(
		IDbFnCtx Ctx
		,IEnumerable<str>? UpperFieldsToUpdate
		,CT Ct
	){
		var NId = T.CodeIdName;
		UpperFieldsToUpdate = UpperFieldsToUpdate??T.Columns.Keys;
		var Clause = T.UpdateClause(UpperFieldsToUpdate);
		var FieldsToUpdateMap = UpperFieldsToUpdate.ToHashSet();
		var Sql =
$"UPDATE {T.Qt(T.DbTblName)} SET {Clause} WHERE {T.QtCol(NId)} = {T.Prm(NId)}";
		var Cmd = await Ctx.PrepareToDispose(SqlCmdMkr, Sql, Ct);
		return async(Entity, Ct)=>{
			var Arg = ArgDict.Mk(T);
			var CodeDict = T.EntityToCodeDict(Entity);
			foreach(var (k,v) in CodeDict){
				if(FieldsToUpdateMap.Contains(k)){
					Arg.AddT(T.Prm(k), v, k);
				}
			}
			await Cmd.Args(Arg).All1d(Ct);
			return NIL;
		};
	}



	/// <param name="FieldsToUpdate">潙null旹更新全部字段</param>
	/// TODO 用批量插入sql 代替for循環
	public async Task<Func<
		IEnumerable<TEntity>
		,CT
		,Task<nil>
	>> FnUpdManyById(
		IDbFnCtx Ctx
		,IEnumerable<str>? FieldsToUpdate
		,CT Ct
	){
		var UpdById = await FnUpdOneById(Ctx, FieldsToUpdate, Ct);
		return async(Entitys, Ct)=>{
			foreach(var e in Entitys){
				await UpdById(e, Ct);
			}
			return NIL;
		};
	}

	public async Task<Func<
		IAsyncEnumerable<TEntity>
		,CT
		,Task<nil>
	>> FnAsyEUpdManyById(
		IDbFnCtx Ctx
		,IEnumerable<str>? UpperFieldsToUpdate
		,CT Ct
	){
		var UpdManyById = await FnUpdManyById(Ctx, UpperFieldsToUpdate, Ct);
		return async(PoAsyE, Ct)=>{
			await UpdManyById(PoAsyE.ToBlockingEnumerable(), Ct);//TODO
			return NIL;
		};
	}


	[Obsolete]
	[Impl]
	public async Task<Func<
		IEnumerable<Id_Dict<TId>>
		,CT
		,Task<nil>
	>> FnUpdManyByIdOld(
		IDbFnCtx Ctx
		,IEnumerable<str> FieldsToUpdate
		,CT Ct
	){
		var T = TblMgr.GetTbl<TEntity>();
		var NId = T.CodeIdName;
		var Clause = T.UpdateClause(FieldsToUpdate);
		var Sql =
$"UPDATE {T.Qt(T.DbTblName)} SET ${Clause} WHERE {T.QtCol(NId)} = {T.Prm(NId)}";

		var Cmd = await Ctx.PrepareToDispose(SqlCmdMkr, Sql, Ct);
		var Fn = async(
			IEnumerable<Id_Dict<TId>> Id_Dicts
			,CT ct
		)=>{
			foreach(var id_dict in Id_Dicts){
				var CodeId = id_dict.Id;
				var CodeDict = id_dict.Dict;
				var DbDict = T.ToDbDict(CodeDict);
				await Cmd.RawArgs(DbDict).AsyE1d(ct).FirstOrDefaultAsync(ct);
			}//~for
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


//TODO TEST
	[Impl]
	public async Task<Func<
		IEnumerable<obj?>
		,CT
		,Task<nil>
	>> FnSoftDelManyByKeys(
		IDbFnCtx Ctx
		,str KeyNameInCode
		,u64 CountPerBatch
		,CT Ct
	){
		if(T.SoftDelCol == null){
			throw new Exception("SoftDeleteCol is null");
		}
		var ParamList = T.Prm(1, CountPerBatch);
		var Sql =
$"""
UPDATE {T.Qt(T.DbTblName)}
SET {T.QtCol(T.SoftDelCol.CodeColName)} = {T.Prm(0,0)[0]}
WHERE {T.QtCol(KeyNameInCode)} IN ({str.Join(",", ParamList)})
AND {T.QtCol(KeyNameInCode)} IS NOT NULL
;
""";
		var ValToSet = T.SoftDelCol.FnDelete(null);
		var Cmd = await Ctx.PrepareToDispose(SqlCmdMkr, Sql, Ct);
		return async(Keys, Ct)=>{
			await using BatchCollector<object?, nil> BatchList = new(async (x, Ct)=>{
				IList<object?> Args = [ValToSet, ..x];
				Args.FillUpTo(CountPerBatch+1, null);
				await Cmd.Args(Args).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
				return NIL;
			});

			foreach(var key in Keys){
				await BatchList.Add(key, Ct);
			}
			await BatchList.End(Ct);
			return NIL;
		};
	}

	[Impl]
	public async Task<Func<
		IEnumerable<TKey>
		,CT
		,Task<nil>
	>> FnSoftDelManyByKeys<TKey>(
		IDbFnCtx Ctx
		,str KeyNameInCode
		,u64 ParamNum
		,CT Ct
	){
		var NonGeneric = await FnSoftDelManyByKeys(Ctx, KeyNameInCode, ParamNum, Ct);
		return async(Keys,Ct)=>{
			var Args = Keys.Select(K=>T.UpperToRaw(K, KeyNameInCode));
			await NonGeneric(Args, Ct);
			return NIL;
		};
	}

/// TODO 用Where Id IN (@0, @1, @2...) 㕥減次芝往返
	public async Task<Func<
		TId
		,CT
		,Task<nil>
	>> FnDeleteOneById(
		IDbFnCtx Ctx
		,CT Ct
	){
		var T = TblMgr.GetTbl<TEntity>();
var Sql = $"DELETE FROM {T.DbTblName} WHERE {T.QtCol(T.CodeIdName)} = ?";

		var Cmd = await Ctx.PrepareToDispose(SqlCmdMkr, Sql, Ct);
		async Task<nil> Fn(
			TId Id
			,CT Ct
		) {
			if (Id is not TId id) {
				throw new Exception("Id is not T_Id id");
			}
			var IdCol = T.Columns[T.CodeIdName];
			var ConvertedId = IdCol.UpperToRaw?.Invoke(Id);
			await Cmd.Args([ConvertedId]).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			return NIL;
		}
		return Fn;
	}


//TODO TEST
[Obsolete("等FnSoftDeleteManyByKeys測試無誤後再照搬")]
	public async Task<Func<
		IEnumerable<object?>
		,CT
		,Task<nil>
	>> FnDeleteManyByKeys(
		IDbFnCtx Ctx
		,str KeyNameInCode
		,u64 ParamNum
		,CT Ct
	){
		var T = TblMgr.GetTbl<TEntity>();
		var Clause = T.NumParamClause(ParamNum-1);
		var Sql =
$"""
DELETE FROM {T.Qt(T.DbTblName)} WHERE {T.QtCol(KeyNameInCode)} IN ${Clause}
AND {T.Qt(KeyNameInCode)} IS NOT NULL;
""";
		var Cmd = await Ctx.PrepareToDispose(SqlCmdMkr, Sql, Ct);
		var Fn = async(
			IEnumerable<object?> Keys
			,CT Ct
		)=>{

			IList<object?> Args = new List<object?>();
			u64 i = 0, j=0;
			foreach(var key in Keys){
				Args.Add(key);
				if(j == ParamNum - 1){
					await Cmd.Args(Args).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
					Args.Clear();
					j = 0;
				}
			i++;j++;}
			if(j > 0){
				await Cmd.Args(Args).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			}
			return NIL;
		};
		return Fn;
	}


	public async Task<Func<
		IEnumerable<TKey>
		,CT
		,Task<nil>
	>> FnDeleteManyByKeys<TKey>(
		IDbFnCtx Ctx
		,str KeyNameInCode
		,u64 ParamNum
		,CT Ct
	){
		var T = TblMgr.GetTbl<TEntity>();
		var NonGeneric = await FnDeleteManyByKeys(Ctx, KeyNameInCode, ParamNum, Ct);
		var Fn = async(
			IEnumerable<TKey> Keys
			,CT Ct
		)=>{
			var Args = Keys.Select(Id => T.UpperToRaw(Id, KeyNameInCode));
			await NonGeneric(Args, Ct);
			return NIL;
		};
		return Fn;
	}

	[Impl]
	public async Task<Func<
		TId
		,obj?
		,CT
		,Task<nil>
	>> FnUpdOneColById(
		IDbFnCtx Ctx
		,str Col
		,CT Ct
	){
		var T = TblMgr.GetTbl<TEntity>();
str NId = T.CodeIdName;
var PTarget = T.Prm("__Target");var PId = T.Prm(NId);
var Sql = $"""
UPDATE {T.Qt(T.DbTblName)}
SET {T.DbColName(Col)} = {PTarget}
WHERE {T.DbColName(NId)} = {PId}
""";
var SqlCmd = await SqlCmdMkr.Prepare(Ctx, Sql, Ct);
		Ctx?.AddToDispose(SqlCmd);
		var Fn = async(TId Id, obj? Target, CT Ct)=>{
			var Arg = ArgDict.Mk()
				.AddRaw(PId, T.UpperToRaw(Id, NId))
				.AddRaw(PTarget, Target)
				.ToDict()
			;
			await SqlCmd.RawArgs(Arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			return NIL;
		};
		return Fn;
	}

	// static str SqlFilterDel(
	// 	ITable Tbl,
	// 	bool IncludeDel
	// ){
	// 	return IncludeDel
	// 		? ""
	// 		: $"AND {Tbl.SqlIsNonDel()}";
	// }

	[Obsolete]
	public async Task<Func<
		IList<TVal>
		,CT
		,IAsyncEnumerable<IStr_Any>
	>> FnScltAllByColInVals<TVal>(
		IDbFnCtx Ctx
		,ITable Tbl
		,str CodeCol
		,OptQry? OptQry
		,CT Ct
	){
		var T = Tbl;
		var numParams = T.NumParams(OptQry?.InParamCnt??1);
		var Sql =
$"""
SELECT * FROM {Tbl.Qt(Tbl.DbTblName)}
WHERE 1=1
AND {T.QtCol(CodeCol)} IN ({str.Join(",", numParams)})
""";
		var SqlCmd = await SqlCmdMkr.Prepare(Ctx, Sql, Ct);
		return (Args ,Ct)=>{
			if(Args.Count < numParams.Count){
				throw new Exception("Args.Count < numParams.Count");
			}
			var Arg = ArgDict.Mk(T)
			.AddManyT(numParams, Args);
			var DbDict = Ctx.RunCmd(SqlCmd, Arg).AsyE1d(Ct);
			return DbDict;
		};
	}
public async Task<Func<
		IList<TCol>
		,CT
		,IAsyncEnumerable<TEntity2>
	>> FnScltAllByColInVals<TEntity2, TCol>(
		IDbFnCtx Ctx
		,ITable Tbl
		,str CodeCol
		,OptQry? OptQry
		,CT Ct
	)
		where TEntity2 : class, new()
	{
		var T = Tbl;
		OptQry??=new();
		var numParams = Tbl.NumParamsEndStart(OptQry.InParamCnt-1);
		var Sql =
$"""
SELECT * FROM {Tbl.Qt(Tbl.DbTblName)}
WHERE 1=1
AND {Tbl.QtCol(CodeCol)} IN ({str.Join(",", numParams)})
""";
		var SqlCmd = await SqlCmdMkr.Prepare(Ctx, Sql, Ct);
		return (Args ,Ct)=>{
			if(Args.Count < numParams.Count){
				throw new Exception("Args.Count < numParams.Count");
			}
			var Arg = ArgDict.Mk(Tbl)
			.AddManyT(numParams, Args);
			var DbDict = Ctx.RunCmd(SqlCmd, Arg).AsyE1d(Ct);
			return DbDict.Select(x=>Tbl.DbDictToEntity<TEntity2>(x));
		};
	}
}
