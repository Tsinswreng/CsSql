using Tsinswreng.CsTreeTest;
using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Test.Domains;

namespace Tsinswreng.CsSql.Test.CsSql.Repo;

/// 為 IRepo.IncludeEntitysByKeys 的兩個 overload 提供測試。</summary>
public partial class TestRepo {
	/// <summary>驗證非泛型 ITable overload 會分組多個 key、過濾 null key,並排除軟刪資料。</summary>
	void RegisterIncludeEntitysByKeys(ITestNode Node) {
		var Register = Node.MkTestFnRegister(
			typeof(TestRepo)
			,[typeof(IRepo<TestWordProp, IdTestWordProp>)]
			,[nameof(IRepo<TestWordProp, IdTestWordProp>.IncludeEntitysByKeys)]
		);
		var R = Register.Register;
		var T = Assert.IsTrue;

		R(
			"IncludeEntitysByKeysUntypedTableGroupsKeysFiltersNullAndExcludesDeleted"
			,async(o)=>{
				return await RunInTxnIfNoCtx(async(Ctx)=>{
					var WordA = new IdTestWord();
					var WordB = new IdTestWord();
					var KeepA = MkIncludeProp(WordA, "keep_a");
					var DeleteA = MkIncludeProp(WordA, "delete_a");
					var KeepB = MkIncludeProp(WordB, "keep_b");
					var KeyA = KeepA.KStr!;
					var KeyB = KeepB.KStr!;
					DeleteA.KStr = KeyA;
					var PropIds = new[]{KeepA.Id, DeleteA.Id, KeepB.Id};

					try {
						// 同一 key 放兩筆資料並軟刪其中一筆,讓過濾及分組行為可在一次查詢中驗證。
						await RepoProp.OrdAdd(Ctx, AsyE(KeepA, DeleteA, KeepB), CT.None);
						await RepoProp.OrdSoftDelById(Ctx, AsyE(DeleteA.Id), CT.None);

						ITable UntypedTable = TblMgr.GetTbl<TestWordProp>();
						var RowsByKey = await RepoProp.IncludeEntitysByKeys<TestWordProp, str>(
							Ctx
							,nameof(TestWordProp.KStr)
							,new OptQry{IncludeDeleted = false}
							,new str?[]{KeyA, null, KeyB}
							,X=>X.KStr!
							,UntypedTable
							,CT.None
						);

						T(RowsByKey.Count == 2);
						T(RowsByKey.TryGetValue(KeyA, out var RowsA));
						T(RowsA is not null && RowsA.Count == 1 && RowsA[0].Id == KeepA.Id);
						T(RowsByKey.TryGetValue(KeyB, out var RowsB));
						T(RowsB is not null && RowsB.Count == 1 && RowsB[0].Id == KeepB.Id);
						return NIL;
					} finally {
						// 清理包含軟刪行在內的全部 fixture,確保失敗路徑也不留下資料。
						await RepoProp.OrdHardDelById(Ctx, AsyE(PropIds), CT.None);
					}
				});
			}
		);

		R(
			"IncludeEntitysByKeysTypedTableIncludesDeletedAndAcceptsEmptyKeys"
			,async(o)=>{
				return await RunInTxnIfNoCtx(async(Ctx)=>{
					var WordId = new IdTestWord();
					var Keep = MkIncludeProp(WordId, "typed_keep");
					var Deleted = MkIncludeProp(WordId, "typed_deleted");

					try {
						await RepoProp.OrdAdd(Ctx, AsyE(Keep, Deleted), CT.None);
						await RepoProp.OrdSoftDelById(Ctx, AsyE(Deleted.Id), CT.None);

						var TypedTable = TblMgr.GetTbl<TestWordProp>();
						var RowsByWord = await RepoProp.IncludeEntitysByKeys<TestWordProp, IdTestWord>(
							Ctx
							,nameof(TestWordProp.WordId)
							,new OptQry{IncludeDeleted = true}
							,new[]{WordId}
							,X=>X.WordId
							,TypedTable
							,CT.None
						);

						T(RowsByWord.TryGetValue(WordId, out var Rows));
						T(Rows is not null && Rows.Count == 2);
						T(Rows!.Any(X=>X.Id == Keep.Id && X.DelAt == 0));
						T(Rows!.Any(X=>X.Id == Deleted.Id && X.DelAt != 0));

						// 空 key 集合應直接得到空字典,不能生成無效 IN 子句或帶回其他資料。
						var Empty = await RepoProp.IncludeEntitysByKeys<TestWordProp, IdTestWord>(
							Ctx
							,nameof(TestWordProp.WordId)
							,new OptQry{IncludeDeleted = true}
							,Array.Empty<IdTestWord>()
							,X=>X.WordId
							,TypedTable
							,CT.None
						);
						T(Empty.Count == 0);
						return NIL;
					} finally {
						await RepoProp.OrdHardDelById(Ctx, AsyE(Keep.Id, Deleted.Id), CT.None);
					}
				});
			}
		);
	}

	/// <summary>建立具唯一值的單詞屬性 fixture,使分組結果可依 ID 精確斷言。</summary>
	static TestWordProp MkIncludeProp(IdTestWord WordId, str Label) {
		var Suffix = Guid.NewGuid().ToString("N");
		return new TestWordProp{
			Id = new IdTestWordProp(),
			WordId = WordId,
			KStr = $"include_by_keys_{Label}_{Suffix}",
			VStr = $"include_by_keys_value_{Label}_{Suffix}",
		};
	}
}
