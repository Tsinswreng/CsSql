namespace Tsinswreng.CsSql;

using System.Linq;
using System.Collections;
using IStr_Any = System.Collections.Generic.IDictionary<str, obj?>;

/// 語句部實現(與 ISqlSplicer.cs 的 Decl 對應):
/// 每個方法 = 拼一段可見 SQL 片段 + 為需要的參數註冊 binder,
/// 循環/列枚舉/對序後綴全在庫內,調用方每子句一行、SQL 形狀可見。
public partial class ISqlSplicer<E>{

	// ■ INSERT 值部:(a, b) VALUES (@a, @b)
	// 每列一個 Many binder:值序列 = 整批該列(缺列補 null);Build 按行數展開 N 份。
	public partial ISqlSplicer<E> Vals(
		IList<str> CodeCols
		,IList<IStr_Any> CodeDicts
	){
		PL();
		for(var i = 0; i < CodeCols.Count; i++){
			if(i > 0){
				C();
			}
			Raw(QtCol(CodeCols[i]));
		}
		PR();
		Raw("VALUES");
		PL();
		for(var i = 0; i < CodeCols.Count; i++){
			if(i > 0){
				C();
			}
			var col = CodeCols[i];
			var P = Prm(col);
			AddSeg(P);
			var Vals = CodeDicts
				.Select(d => d.TryGetValue(col, out var v) ? v : null)
				.ToList();
			ParamAutoBinders.Add(new SqlArgBinderFactory(P, Tbl, col, SharedManyCtx).Many(Vals));
		}
		PR();
		return this;
	}

	// ■ UPDATE SET 子句:SET a = @a, b = @b(含 SET 詞頭)
	// 每列一個 Many binder:值序列 = 整批該列(缺列補 null);配合 UpdateT()/Where1()/And().Bool(...) 使用。
	public partial ISqlSplicer<E> Set(
		IList<str> CodeCols
		,IList<IStr_Any> CodeDicts
	){
		Raw("SET");
		for(var i = 0; i < CodeCols.Count; i++){
			if(i > 0){
				C();
			}
			var col = CodeCols[i];
			Raw(QtCol(col)).Raw("=");
			var P = Prm(col);
			AddSeg(P);
			var Vals = CodeDicts
				.Select(d => d.TryGetValue(col, out var v) ? v : null)
				.ToList();
			ParamAutoBinders.Add(new SqlArgBinderFactory(P, Tbl, col, SharedManyCtx).Many(Vals));
		}
		return this;
	}

	// ■ UPDATE SET 單列單值:SET col = @col
	// 值已 raw(不帶 Tbl,不再 Upper→Raw——如軟刪值 = SoftDelCol.FnDelete(null) 的結果)。
	public partial ISqlSplicer<E> Set(
		str CodeCol
		,obj? RawVal
	){
		Raw("SET");
		Raw(QtCol(CodeCol)).Raw("=");
		var P = Prm(CodeCol);
		AddSeg(P);
		ParamAutoBinders.Add(new SqlArgBinderFactory(P).One(RawVal));
		return this;
	}

	// ■ 異構字典 UPDATE:逐對 `UPDATE t SET 列=@u_{i}_{j}... WHERE CodeId=@id_{i}`,對間 ';' 拼一命令。
	// DbDicts 值已 raw(不帶 Tbl,避免二次 Upper→Raw);主鍵值為 code 層(帶 Tbl+CodeIdName 轉換)。
	// 空對跳過、主鍵列(CodeIdName 或其 Db 名)永不出現在 SET——與原 BatOrdUpdByDbDictCore 語義一致。
	public partial ISqlSplicer<E> UpdEach(
		str CodeIdName
		,IList<obj?> Ids
		,IList<IStr_Any> DbDicts
	){
		if(Ids.Count != DbDicts.Count){
			throw new ArgumentException("Ids count must equal DbDicts count");
		}
		var dbIdColName = Tbl.DbColName(CodeIdName);
		i32 stmtCnt = 0;
		for(i32 i = 0; i < DbDicts.Count; i++){
			var DbDict = DbDicts[i];
			// 跳過空對、剔除主鍵列(dict 鍵可能用 code 名或 Db 名兩種拼法)
			var Fields = DbDict
				.Where(kv => kv.Key != CodeIdName && kv.Key != dbIdColName)
				.ToList();
			if(Fields.Count == 0){
				continue;
			}
			if(stmtCnt > 0){
				Raw(";");
			}
			UpdateT().Set();
			for(i32 j = 0; j < Fields.Count; j++){
				if(j > 0){
					C();
				}
				var(dbCol, rawVal) = Fields[j];
				Raw(Tbl.Qt(dbCol)).Raw("=");
				var P = Prm($"u_{i}_{j}");
				AddSeg(P);
				// 值已 raw:不帶 Tbl
				ParamAutoBinders.Add(new SqlArgBinderFactory(P).One(rawVal));
			}
			var PId = Prm($"id_{i}");
			Raw($" WHERE {QtCol(CodeIdName)} = ").AddSeg(PId);
			// 主鍵值為 code 層:帶 Tbl+CodeIdName 走 Upper→Raw
			ParamAutoBinders.Add(new SqlArgBinderFactory(PId, Tbl, CodeIdName).One(Ids[i]));
			stmtCnt++;
		}
		return this;
	}

	// ■ WHERE 列 IN 值列表:WHERE col IN (@_0,@_1...)
	// 語詞對應裸 SQL:軟刪=UpdateT().Set(軟刪列,raw).WhereIn(...)、硬刪=DelFromT().WhereIn(...)。
	// 每個值為 code 層(帶 Tbl+CodeCol 轉換),與原 SoftDelIn/HardDelIn 的 IN 部分同義。
	public partial ISqlSplicer<E> WhereIn(
		str CodeCol
		,IList<obj?> UpperVals
	){
		Raw($"WHERE {QtCol(CodeCol)} IN (");
		for(i32 i = 0; i < UpperVals.Count; i++){
			if(i > 0){
				C();
			}
			var P = Tbl.NumParam((u64)i);
			AddSeg(P);
			ParamAutoBinders.Add(new SqlArgBinderFactory(P, Tbl, CodeCol).One(UpperVals[i]));
		}
		PR();
		return this;
	}

	// ■ 產出執行端入參:
	// Sql = DuplicateSql(N):模板按各同步 Many binder 的公共全量長度 N 展開 N 份 ';' 拼(無 Many 則 1 份);
	// Args = One 按 ToOfst(i) 綁 @name/@name__1..(每份一條) + Many 綁 @name__0..@name__N-1,
	// 與 ToSqlStrAtOfst(i) 的參數名逐份對齊。
	// 只支持同步 binder(One/同步 Many);異步 Many(流式 IAsyncEnumerable)應走舊 RunDupliSql。
	public partial ISqlEtArg Build(){
		var OneBinders = new List<IParamAutoBinderOneBatch>();
		var ManyBinders = new List<IParamAutoBinderMulti>();
		foreach(var b in ParamAutoBinders){
			if(b is IParamAutoBinderMultiAsy){
				throw new NotSupportedException(
					$"Bind(): 不支持異步 Many binder({b.GetType().Name});異步流式請改用 RunDupliSql。"
				);
			}
			if(b is IParamAutoBinderOneBatch ob){
				OneBinders.Add(ob);
			}else if(b is IParamAutoBinderMulti m){
				ManyBinders.Add(m);
			}else{
				throw new NotSupportedException($"未知 binder 類型: {b.GetType().Name}");
			}
		}

		u64 N = 1;
		var Batches = new List<IList>();
		if(ManyBinders.Count > 0){
			// 第一個 Many 拉全量 → N;其餘 Many 拉全量校驗對齊
			var First = TakeAll(ManyBinders[0]);
			N = (u64)First.Count;
			if(N == 0){
				throw new InvalidOperationException($"Build(): Many binder 值源為空。Tbl={Tbl.DbTblName}");
			}
			Batches.Add(First);
			for(var i = 1; i < ManyBinders.Count; i++){
				var Batch = TakeAll(ManyBinders[i]);
				if((u64)Batch.Count != N){
					throw new InvalidOperationException(
						$"Build(): Many binder 公共長度不一致。Tbl={Tbl.DbTblName}, 期望={N}, 實際={Batch.Count}"
					);
				}
				Batches.Add(Batch);
			}
		}

		var Sql = DuplicateSql(N);
		var Args = ArgDict.Mk();
		foreach(var ob in OneBinders){
			ob.BindBatch(Args, N);
		}
		for(var i = 0; i < ManyBinders.Count; i++){
			ManyBinders[i].BindBatch(Args, Batches[i]);
		}
		return new SqlEtArg{ Sql = Sql, Args = Args };
	}

	private static IList TakeAll(IParamAutoBinderMulti multi){
		if(!multi.TryTakeBatchArgs(u64.MaxValue, out var Batch)){
			return new List<obj?>();
		}
		return Batch;
	}
}