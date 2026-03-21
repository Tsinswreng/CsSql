namespace Tsinswreng.CsSql;

using System.Data;


using Tsinswreng.CsCore;
using Tsinswreng.CsTools;
using Tsinswreng.CsPage;
using System.Collections;
using System.Diagnostics;
using Str_Any = System.Collections.Generic.Dictionary<str, obj?>;
using IStr_Any = System.Collections.Generic.IDictionary<str, obj?>;
using Tsinswreng.CsStrAcc;

//using T = Bo_Word;
//TODO 拆分ⁿ使更通用化
//TODO 分頁
public partial class SqlRepo<
	TEntity, TId
>
	:IRepo<TEntity, TId>
	where TEntity: class, new()
{

	public ITblMgr TblMgr{get;set;}
	public ISqlCmdMkr SqlCmdMkr{get;set;}
	public IPropAccessorMgr PropAccessorMgr{get;set;}

	public SqlRepo(
		ITblMgr TblMgr
		,ISqlCmdMkr SqlCmdMkr
		,IPropAccessorMgr PropAccessorMgr
	){
		this.PropAccessorMgr = PropAccessorMgr;
		this.TblMgr = TblMgr;
		this.SqlCmdMkr = SqlCmdMkr;
	}

	public ITable<TEntity> T => TblMgr.GetTbl<TEntity>();
	
	public Task<IAsyncEnumerable<TEntity?>> GetManyInIdsWithDel(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	){
		IList<IParam> Params = [];
		var sqlD = FnSqlDuplicator.Mk((Cnt)=>{
			Params = T.NumParams(Cnt);
			return $"SELECT * FROM {T.Qt(T.DbTblName)} WHERE {T.QtCol(T.CodeIdName)} IN ({str.Join(", ", Params)})" ;
		});
		var bat = SqlCmdMkr.AutoBatch<TId, IAsyncEnumerable<TEntity?>>(
			Ctx, sqlD,
			async(z, Ids, Ct)=>{
				var Args = ArgDict.Mk(T).AddManyT(Params, Ids, T.CodeIdName);
				var RawDicts = z.SqlCmd.Args(Args).AsyE1d(Ct);
				return RawDicts.Select(x=>T.DbDictToEntity(x));
			}
		);

		async IAsyncEnumerable<TEntity?> Run(){
			await using var Bat = bat;
			await foreach(var id in Ids.WithCancellation(Ct)){
				var oneBatch = await Bat.Add(id, Ct);
				if(oneBatch is null){
					continue;
				}
				await foreach(var item in oneBatch.WithCancellation(Ct)){
					yield return item;
				}
			}
			var tailBatch = await Bat.End(Ct);
			if(tailBatch is not null){
				await foreach(var item in tailBatch.WithCancellation(Ct)){
					yield return item;
				}
			}
		}

		return Task.FromResult<IAsyncEnumerable<TEntity?>>(Run());
	}

	public async Task<IAsyncEnumerable<TEntity?>> SlctManyInIds(
		IDbFnCtx Ctx, IEnumerable<TId> Ids
		,CT Ct
	){
		IList<IParam> Params = [];
		var sqlD = FnSqlDuplicator.Mk((Cnt)=>{
			Params = T.NumParams(Cnt);
			return 
$"""
SELECT * FROM {T.Qt(T.DbTblName)} WHERE {T.QtCol(T.CodeIdName)} IN ({str.Join(", ", Params)})
{T.AndSqlIsNonDel()}
""" ;
		});
		var bat = SqlCmdMkr.AutoBatch<TId, IAsyncEnumerable<TEntity?>>(
			Ctx, sqlD,
			async(z, Ids, Ct)=>{
				var Args = ArgDict.Mk(T).AddManyT(Params, Ids, T.CodeIdName);
				var RawDicts = z.SqlCmd.Args(Args).AsyE1d(Ct);
				return RawDicts.Select(x=>T.DbDictToEntity(x));
			}
		);
		var R = bat.AddToEnd(Ids, Ct);
		return R.Flat();
	}


	public Task<IAsyncEnumerable<TEntity?>> BatGetById(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	){
		var Sql = T.SqlSplicer().Select("*").From().Where1()
		.And().Bool(T.CodeIdName, "=", out var PId);

		var bat = AutoBatch<TId, IAsyncEnumerable<TEntity?>>.Mk(
			Ctx, SqlCmdMkr, Sql,
			async(z, Ids, Ct)=>{
				var Args = ArgDict.Mk(T).AddManyT(PId, Ids, T.CodeIdName);
				var RawDicts = z.SqlCmd.Args(Args).AsyE1dWithNull(Ct);
				return RawDicts.Select(x=>x is null? null : T.DbDictToEntity<TEntity>(x));
			}
		);

		async IAsyncEnumerable<TEntity?> Run(){
			await using var Bat = bat;
			await foreach(var id in Ids.WithCancellation(Ct)){
				var oneBatch = await Bat.Add(id, Ct);
				if(oneBatch is null){
					continue;
				}
				await foreach(var item in oneBatch.WithCancellation(Ct)){
					yield return item;
				}
			}
			var tailBatch = await Bat.End(Ct);
			if(tailBatch is not null){
				await foreach(var item in tailBatch.WithCancellation(Ct)){
					yield return item;
				}
			}
		}

		return Task.FromResult<IAsyncEnumerable<TEntity?>>(Run());
	}

	public Task<IAsyncEnumerable<TEntity>> GetAll(
		IDbFnCtx Ctx, CT Ct
	){
		throw new NotImplementedException();
	}
	
	public Task<IAsyncEnumerable<TAgg>> GetAllAgg<TAgg>(
		IDbFnCtx Ctx, CT Ct
	){
		throw new NotImplementedException();
	}

	public async Task<IAsyncEnumerable<TAgg?>> BatGetAggById<TAgg>(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	)
		where TAgg: class
	{
		async IAsyncEnumerable<TId> ToAsyncIds(IEnumerable<TId> Src){
			foreach(var id in Src){
				yield return id;
			}
		}

		var aggReg = TblMgr.GetAgg<TAgg>();
		if(aggReg.RootEntityType != typeof(TEntity)){
			throw new Exception($"Agg root type mismatch. Agg={typeof(TAgg)}, ExpectedRoot={typeof(TEntity)}, RegisteredRoot={aggReg.RootEntityType}");
		}
		if(aggReg.RootIdType != typeof(TId)){
			throw new Exception($"Agg root id type mismatch. Agg={typeof(TAgg)}, ExpectedId={typeof(TId)}, RegisteredId={aggReg.RootIdType}");
		}

		u64 InBatchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 50ul : 500ul;

		async Task<IList<TAgg?>> HandleOneBatch(IList<TId> OrderedBatchIds, CT Ct){
			var rootsAsy = await GetManyInIdsWithDel(Ctx, ToAsyncIds(OrderedBatchIds), Ct);
			var rootById = new Dictionary<object, TEntity>();
			var rootIdSet = new HashSet<TId>();
			await foreach(var root in rootsAsy.WithCancellation(Ct)){
				if(root is null){
					continue;
				}
				var keyObj = aggReg.FnGetIdFromRootObj(root);
				if(keyObj is null){
					continue;
				}
				if(keyObj is not TId key){
					throw new Exception($"Agg root key type mismatch. Agg={typeof(TAgg)}, Root={typeof(TEntity)}, Key={keyObj.GetType()}, ExpectedKey={typeof(TId)}");
				}
				rootById[key] = root;
				rootIdSet.Add(key);
			}

			var qryCtx = new AggQryCtx();
			if(rootIdSet.Count > 0){
				var rootIds = rootIdSet.ToList();
				var optQry = new OptQry{ InParamCnt = (u64)rootIds.Count };
				foreach(var include in aggReg.Includes){
					var slctByIn = await FnScltAllByColInVals<TId>(Ctx, include.Tbl, include.FKeyCodeCol, optQry, Ct);
					var dbAsy = await slctByIn(rootIds, Ct);
					await foreach(var dbDict in dbAsy.WithCancellation(Ct)){
						var codeDict = include.Tbl.ToCodeDict(dbDict);
						var entity = include.FnNewEntityObj();
						include.Tbl.AssignEntityByCodeDict(include.EntityType, entity, codeDict);
						var keyObj = include.FnFKeyToRootIdObj(entity);
						if(keyObj is null){
							continue;
						}
						if(include.RelKind == EAggRelKind.OneToOne
							&& qryCtx.GetOne(include.EntityType, keyObj) is not null
						){
							throw new Exception($"OneToOne include got duplicate rows. Agg={typeof(TAgg)}, Include={include.EntityType}, Key={keyObj}");
						}
						qryCtx.Add(include.EntityType, keyObj, entity);
					}
				}
			}

			var ans = new List<TAgg?>(OrderedBatchIds.Count);
			foreach(var id in OrderedBatchIds){
				if(!rootById.TryGetValue(id!, out var root)){
					ans.Add(null);
					continue;
				}
				var agg = (TAgg)aggReg.FnAssembleAggObj(root, qryCtx);
				ans.Add(agg);
			}
			return ans;
		}

		async IAsyncEnumerable<TAgg?> Fn(IAsyncEnumerable<TAgg?> Src){
			await foreach(var item in Src.WithCancellation(Ct)){
				yield return item;
			}
		}

		async IAsyncEnumerable<TAgg?> Run(){
			var batchIds = new List<TId>((i32)InBatchSize);
			await foreach(var id in Ids.WithCancellation(Ct)){
				batchIds.Add(id);
				if((u64)batchIds.Count < InBatchSize){
					continue;
				}
				var batchAns = await HandleOneBatch(batchIds, Ct);
				foreach(var item in batchAns){
					yield return item;
				}
				batchIds = new List<TId>((i32)InBatchSize);
			}

			if(batchIds.Count > 0){
				var batchAns = await HandleOneBatch(batchIds, Ct);
				foreach(var item in batchAns){
					yield return item;
				}
			}
		}

		return Fn(Run());
	}




	

	public delegate Task<IDictionary<TKey, IList<TPo>>> TFnIncludeEntitysByKeys<TKey, TPo>(
		ITable Tbl, Func<TPo, TKey> FnMemb, IEnumerable<TKey?> Keys, CT Ct
	);

/*
Func<
		ITable, Func<TPo, TKey>, IEnumerable<TKey>
		,CT, Task<IDictionary<TKey, IList<TPo>>>
	>
 */
	protected async Task<TFnIncludeEntitysByKeys<TKey, TPo>> FnIncludeEntitysByKeys<TPo, TKey>(
		IDbFnCtx Ctx
		,ITable Tbl
		,str CodeCol
		,OptQry? OptQry
		,CT Ct
	)where TPo: new(){
		var fn = await FnScltAllByColInVals<TKey>(Ctx, Tbl, CodeCol, OptQry, Ct);
		return async(Tbl, Memb, Keys, Ct)=>{
			IList<TKey> KeyList = Keys.Where(x=>x is not null).ToList()!;
			var poPage = await fn(KeyList, Ct);
			var dicts = await poPage.ToListAsync(Ct);
			var pos = dicts.Select(x=>Tbl.DbDictToEntity<TPo>(x));
			IDictionary<TKey, IList<TPo>> posByKey = pos.GroupBy(Memb).ToDictionary(g=>g.Key, g=>(IList<TPo>)g.ToList());
			return posByKey;
		};
	}

	public async Task<IDictionary<TKey, IList<TPo>>> IncludeEntitysByKeys<TPo, TKey>(
		IDbFnCtx Ctx
		,str CodeCol
		,OptQry? OptQry
		,IEnumerable<TKey?> Keys
		,Func<TPo, TKey> FnMemb
		,ITable Tbl
		,CT Ct
	)where TPo: new(){
		//var keyList = Keys.AsOrToList();
		// 自動把OptQry之ParamCnt設成 Keys.Count?
		var fn = await FnIncludeEntitysByKeys<TPo, TKey>(Ctx, Tbl, CodeCol, OptQry, Ct);
		return await fn(Tbl, FnMemb, Keys, Ct);
	}

	public async Task<IDictionary<TKey, IList<TPo>>> IncludeEntitysByKeys<TPo, TKey>(
		IDbFnCtx Ctx
		,str CodeCol
		,OptQry? OptQry
		,IEnumerable<TKey?> Keys
		,Func<TPo, TKey> FnMemb
		,ITable<TPo> Tbl
		,CT Ct
	)where TPo: new(){
		//var keyList = Keys.AsOrToList();
		// 自動把OptQry之ParamCnt設成 Keys.Count?
		var fn = await FnIncludeEntitysByKeys<TPo, TKey>(Ctx, Tbl, CodeCol, OptQry, Ct);
		return await fn(Tbl, FnMemb, Keys, Ct);
	}

	public async Task<IRespBatInsert> BatAdd(IDbFnCtx Ctx, IAsyncEnumerable<TEntity> Ents, CT Ct){
		u64 BatchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 1ul : 500ul;
		var Cols = T.Columns.Keys.ToList();
		var CmdByCnt = new Dictionary<u64, ISqlCmd>();

		str MkSql(u64 Cnt){
			var Stmts = new List<str>((i32)Cnt);
			foreach(var i in Enumerable.Range(0, (i32)Cnt)){
				var Idx = (u64)i;
				var Fields = str.Join(", ", Cols.Select(x=>T.QtCol(x)));
				var Values = str.Join(", ", Cols.Select(x=>T.NumFieldParam(x, Idx).ToString()));
				Stmts.Add($"INSERT INTO {T.Qt(T.DbTblName)} ({Fields}) VALUES ({Values})");
			}
			return str.Join(";\n", Stmts);
		}

		async Task<ISqlCmd> GetCmd(u64 Cnt, CT Ct){
			if(CmdByCnt.TryGetValue(Cnt, out var Got)){
				return Got;
			}
			var Cmd = await SqlCmdMkr.Prepare(Ctx, MkSql(Cnt), Ct);
			Ctx.AddToDispose(Cmd);
			CmdByCnt[Cnt] = Cmd;
			return Cmd;
		}

		await using var Batch = new BatchCollector<TEntity, nil>(async(BatchEnts, Ct)=>{
			var Cnt = (u64)BatchEnts.Count;
			var Arg = new Dictionary<str, obj?>();
			for(i32 i = 0; i < BatchEnts.Count; i++){
				var Ent = BatchEnts[i];
				var DbDict = T.ToDbDict(T.EntityToCodeDict(Ent));
				foreach(var (k, v) in DbDict){
					Arg[T.NumFieldParam(k, (u64)i).Name] = v;
				}
			}
			var Cmd = await GetCmd(Cnt, Ct);
			await Cmd.RawArgs(Arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			return NIL;
		}, BatchSize);

		await foreach(var Ent in Ents.WithCancellation(Ct)){
			await Batch.Add(Ent, Ct);
		}
		await Batch.End(Ct);

		return new RespBatInsert();
	}

	public async Task<IRespBatUpd> BatUpdById(IDbFnCtx Ctx, IAsyncEnumerable<TEntity> Ents, CT Ct){
		var fieldsToUpdate = T.Columns.Keys.Where(x=>x != T.CodeIdName).ToList();
		if(fieldsToUpdate.Count == 0){
			return new RespUpd();
		}

		u64 BatchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 1ul : 500ul;
		var CmdByCnt = new Dictionary<u64, ISqlCmd>();

		str MkSql(u64 Cnt){
			var Stmts = new List<str>((i32)Cnt);
			foreach(var i in Enumerable.Range(0, (i32)Cnt)){
				var Idx = (u64)i;
				var Clause = str.Join(", ", fieldsToUpdate.Select(x=>$"{T.QtCol(x)} = {T.NumFieldParam(x, Idx)}"));
				var PId = T.NumFieldParam(T.CodeIdName, Idx);
				Stmts.Add($"UPDATE {T.Qt(T.DbTblName)} SET {Clause} WHERE {T.QtCol(T.CodeIdName)} = {PId}");
			}
			return str.Join(";\n", Stmts);
		}

		async Task<ISqlCmd> GetCmd(u64 Cnt, CT Ct){
			if(CmdByCnt.TryGetValue(Cnt, out var Got)){
				return Got;
			}
			var Cmd = await SqlCmdMkr.Prepare(Ctx, MkSql(Cnt), Ct);
			Ctx.AddToDispose(Cmd);
			CmdByCnt[Cnt] = Cmd;
			return Cmd;
		}

		await using var Batch = new BatchCollector<TEntity, nil>(async(BatchEnts, Ct)=>{
			var Cnt = (u64)BatchEnts.Count;
			var Arg = new Dictionary<str, obj?>();
			for(i32 i = 0; i < BatchEnts.Count; i++){
				var Ent = BatchEnts[i];
				var DbDict = T.ToDbDict(T.EntityToCodeDict(Ent));
				foreach(var Col in fieldsToUpdate){
					if(DbDict.TryGetValue(Col, out var Val)){
						Arg[T.NumFieldParam(Col, (u64)i).Name] = Val;
					}
				}
				Arg[T.NumFieldParam(T.CodeIdName, (u64)i).Name] = DbDict[T.CodeIdName];
			}
			var Cmd = await GetCmd(Cnt, Ct);
			await Cmd.RawArgs(Arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			return NIL;
		}, BatchSize);

		await foreach(var Ent in Ents.WithCancellation(Ct)){
			await Batch.Add(Ent, Ct);
		}
		await Batch.End(Ct);

		return new RespUpd();
	}

	public async Task<IRespBatUpd> BatUpdByDbDict(
		IDbFnCtx Ctx
		,IAsyncEnumerable<TId> Ids
		,IAsyncEnumerable<IStr_Any> Dicts
		,CT Ct
	){
		u64 BatchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 1ul : 500ul;
		var CmdBySql = new Dictionary<str, ISqlCmd>();
		var dbIdColName = T.DbCol(T.CodeIdName);

		async Task<ISqlCmd> GetCmd(str Sql, CT Ct){
			if(CmdBySql.TryGetValue(Sql, out var Got)){
				return Got;
			}
			var Cmd = await SqlCmdMkr.Prepare(Ctx, Sql, Ct);
			Ctx.AddToDispose(Cmd);
			CmdBySql[Sql] = Cmd;
			return Cmd;
		}

		await using var Batch = new BatchCollector<(IStr_Any Dict, TId Id), nil>(async(BatchItems, Ct)=>{
			var Stmts = new List<str>(BatchItems.Count);
			var Arg = new Dictionary<str, obj?>();

			for(i32 i = 0; i < BatchItems.Count; i++){
				var (DbDict, Id) = BatchItems[i];
				var SetSegs = new List<str>();
				i32 j = 0;
				foreach(var (DbColName, RawVal) in DbDict){
					if(DbColName == dbIdColName || DbColName == T.CodeIdName){
						continue;
					}
					var P = T.Prm($"u_{i}_{j}");
					SetSegs.Add($"{T.Qt(DbColName)} = {P}");
					Arg[P.Name] = RawVal;
					j++;
				}

				if(SetSegs.Count == 0){
					continue;
				}

				var PId = T.Prm($"id_{i}");
				Arg[PId.Name] = T.UpperToRaw(Id, T.CodeIdName);
				var Clause = str.Join(", ", SetSegs);
				Stmts.Add($"UPDATE {T.Qt(T.DbTblName)} SET {Clause} WHERE {T.QtCol(T.CodeIdName)} = {PId}");
			}

			if(Stmts.Count == 0){
				return NIL;
			}

			var Sql = str.Join(";\n", Stmts);
			var Cmd = await GetCmd(Sql, Ct);
			await Cmd.RawArgs(Arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			return NIL;
		}, BatchSize);

		await using var DictEtor = Dicts.GetAsyncEnumerator(Ct);
		await using var IdEtor = Ids.GetAsyncEnumerator(Ct);
		while(true){
			var HasDict = await DictEtor.MoveNextAsync();
			var HasId = await IdEtor.MoveNextAsync();
			if(HasDict != HasId){
				throw new ArgumentException("Dicts count must equal to Ids count");
			}
			if(!HasDict){
				break;
			}
			await Batch.Add((DictEtor.Current, IdEtor.Current), Ct);
		}
		await Batch.End(Ct);

		return new RespUpd();
	}
	
	public Task<IRespBatUpd> BatUpdByCodeDict(
		IDbFnCtx Ctx
		,IAsyncEnumerable<TId> Ids
		,IAsyncEnumerable<IStr_Any> Dicts
		,CT Ct
	){
		var DbDicts = Dicts.Select(x=>T.ToDbDict(x));
		return BatUpdByDbDict(Ctx, Ids, DbDicts, Ct);
	}

	public async Task<ISoftDelInId> SoftDelInId(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct
	){
		if(T.SoftDelCol is null){
			throw new Exception("SoftDeleteCol is null");
		}
		u64 BatchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 50ul : 500ul;
		var CmdByCnt = new Dictionary<u64, ISqlCmd>();
		var valToSet = T.SoftDelCol.FnDelete(null);

		str MkSql(u64 Cnt){
			var IdParams = T.NumParams(Cnt).ToList();
			var PSoft = T.Prm("__SoftDelVal");
			return $"UPDATE {T.Qt(T.DbTblName)} SET {T.QtCol(T.SoftDelCol.CodeColName)} = {PSoft} WHERE {T.QtCol(T.CodeIdName)} IN ({str.Join(", ", IdParams)})";
		}

		async Task<ISqlCmd> GetCmd(u64 Cnt, CT Ct){
			if(CmdByCnt.TryGetValue(Cnt, out var Got)){
				return Got;
			}
			var Cmd = await SqlCmdMkr.Prepare(Ctx, MkSql(Cnt), Ct);
			Ctx.AddToDispose(Cmd);
			CmdByCnt[Cnt] = Cmd;
			return Cmd;
		}

		await using var Batch = new BatchCollector<TId, nil>(async(BatchIds, Ct)=>{
			var Cnt = (u64)BatchIds.Count;
			var IdParams = T.NumParams(Cnt).ToList();
			var Arg = ArgDict.Mk(T)
				.AddManyT(IdParams, BatchIds, T.CodeIdName)
				.AddRaw(T.Prm("__SoftDelVal"), valToSet)
				.ToDict();
			var Cmd = await GetCmd(Cnt, Ct);
			await Cmd.RawArgs(Arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			return NIL;
		}, BatchSize);

		await foreach(var Id in Ids.WithCancellation(Ct)){
			await Batch.Add(Id, Ct);
		}
		await Batch.End(Ct);
		return new SoftDelInId();
	}

	public async Task<IHardDelInId> HardDelInId(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct
	){
		u64 BatchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 50ul : 500ul;
		var CmdByCnt = new Dictionary<u64, ISqlCmd>();

		str MkSql(u64 Cnt){
			var IdParams = T.NumParams(Cnt).ToList();
			return $"DELETE FROM {T.Qt(T.DbTblName)} WHERE {T.QtCol(T.CodeIdName)} IN ({str.Join(", ", IdParams)})";
		}

		async Task<ISqlCmd> GetCmd(u64 Cnt, CT Ct){
			if(CmdByCnt.TryGetValue(Cnt, out var Got)){
				return Got;
			}
			var Cmd = await SqlCmdMkr.Prepare(Ctx, MkSql(Cnt), Ct);
			Ctx.AddToDispose(Cmd);
			CmdByCnt[Cnt] = Cmd;
			return Cmd;
		}

		await using var Batch = new BatchCollector<TId, nil>(async(BatchIds, Ct)=>{
			var Cnt = (u64)BatchIds.Count;
			var IdParams = T.NumParams(Cnt).ToList();
			var Arg = ArgDict.Mk(T).AddManyT(IdParams, BatchIds, T.CodeIdName).ToDict();
			var Cmd = await GetCmd(Cnt, Ct);
			await Cmd.RawArgs(Arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			return NIL;
		}, BatchSize);

		await foreach(var Id in Ids.WithCancellation(Ct)){
			await Batch.Add(Id, Ct);
		}
		await Batch.End(Ct);
		return new HardDelInId();
	}

	public async Task<IBatSoftDel> BatSoftDelById(IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct){
		if(T.SoftDelCol is null){
			throw new Exception("SoftDeleteCol is null");
		}

		u64 BatchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 1ul : 500ul;
		var CmdByCnt = new Dictionary<u64, ISqlCmd>();
		var valToSet = T.SoftDelCol.FnDelete(null);

		str MkSql(u64 Cnt){
			var IdParams = T.NumParams(Cnt).ToList();
			var PSoft = T.Prm("__SoftDelVal");
			return $"UPDATE {T.Qt(T.DbTblName)} SET {T.QtCol(T.SoftDelCol.CodeColName)} = {PSoft} WHERE {T.QtCol(T.CodeIdName)} IN ({str.Join(", ", IdParams)})";
		}

		async Task<ISqlCmd> GetCmd(u64 Cnt, CT Ct){
			if(CmdByCnt.TryGetValue(Cnt, out var Got)){
				return Got;
			}
			var Cmd = await SqlCmdMkr.Prepare(Ctx, MkSql(Cnt), Ct);
			Ctx.AddToDispose(Cmd);
			CmdByCnt[Cnt] = Cmd;
			return Cmd;
		}

		await using var Batch = new BatchCollector<TId, nil>(async(BatchIds, Ct)=>{
			var Cnt = (u64)BatchIds.Count;
			var Arg = new Dictionary<str, obj?>();
			var IdParams = T.NumParams(Cnt).ToList();
			Arg[T.Prm("__SoftDelVal").Name] = valToSet;
			for(i32 i = 0; i < BatchIds.Count; i++){
				Arg[IdParams[i].Name] = T.UpperToRaw(BatchIds[i], T.CodeIdName);
			}
			var Cmd = await GetCmd(Cnt, Ct);
			await Cmd.RawArgs(Arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			return NIL;
		}, BatchSize);

		await foreach(var Id in Ids.WithCancellation(Ct)){
			await Batch.Add(Id, Ct);
		}
		await Batch.End(Ct);

		return new BatSoftDel();
	}

	public async Task<IBatHardDel> BatHardDelById(IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct){
		u64 BatchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 1ul : 500ul;
		var CmdByCnt = new Dictionary<u64, ISqlCmd>();

		str MkSql(u64 Cnt){
			var IdParams = T.NumParams(Cnt).ToList();
			return $"DELETE FROM {T.Qt(T.DbTblName)} WHERE {T.QtCol(T.CodeIdName)} IN ({str.Join(", ", IdParams)})";
		}

		async Task<ISqlCmd> GetCmd(u64 Cnt, CT Ct){
			if(CmdByCnt.TryGetValue(Cnt, out var Got)){
				return Got;
			}
			var Cmd = await SqlCmdMkr.Prepare(Ctx, MkSql(Cnt), Ct);
			Ctx.AddToDispose(Cmd);
			CmdByCnt[Cnt] = Cmd;
			return Cmd;
		}

		await using var Batch = new BatchCollector<TId, nil>(async(BatchIds, Ct)=>{
			var Cnt = (u64)BatchIds.Count;
			var Arg = new Dictionary<str, obj?>();
			var IdParams = T.NumParams(Cnt).ToList();
			for(i32 i = 0; i < BatchIds.Count; i++){
				Arg[IdParams[i].Name] = T.UpperToRaw(BatchIds[i], T.CodeIdName);
			}
			var Cmd = await GetCmd(Cnt, Ct);
			await Cmd.RawArgs(Arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			return NIL;
		}, BatchSize);

		await foreach(var Id in Ids.WithCancellation(Ct)){
			await Batch.Add(Id, Ct);
		}
		await Batch.End(Ct);

		return new BatHardDel();
	}

	public Task<IRespBatUpdAgg> BatAddAgg<TAgg>(IDbFnCtx Ctx, IAsyncEnumerable<TAgg> NewAgg, CT Ct) {
		throw new NotImplementedException();
	}

	public Task<RespBatHardDelAgg> BatHardDelAggById<TAgg>(IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct) {
		throw new NotImplementedException();
	}

	public Task<RespBatSoftDelAgg> BatSoftDelAggById<TAgg>(IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct) {
		throw new NotImplementedException();
	}
}
