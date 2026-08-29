namespace Tsinswreng.CsSql.Test.Domains;

/// <summary>
/// 測試域表定義初始化。只依賴 ITblMgr 接口,不綁定具體 DB(Sqlite/Postgres 由 exe 入口注入)。
/// 所有表主鍵均為強類型 Id,存 BLOB,由代碼生成(CsSql 不支持自增 id)。
/// </summary>
public static class TestTblMgrIniter {
	/// <summary>把所有測試表與聚合註冊進 mgr。DB 無關。</summary>
	public static ITblMgr Init(ITblMgr mgr) {
		var mapper = TestDictMapper.Inst;

		// ===== AllBasicTypes:無軟刪,覆蓋基礎類型映射 =====
		var tbl = Table.FnSetTbl<PoAllBasicTypes>(mapper)("AllBasicTypes");
		tbl.Tbl.CodeIdName = nameof(PoAllBasicTypes.Id);

		tbl.Col(nameof(PoAllBasicTypes.Id))
			.Type<byte[], byte[]>("BLOB")
			.NotNull()
			.AdditionalSqls(["PRIMARY KEY"]);
		tbl.Col(nameof(PoAllBasicTypes.U8Val)).Type<byte, byte>("INTEGER");
		tbl.Col(nameof(PoAllBasicTypes.I8Val)).Type<sbyte, sbyte>("INTEGER");
		tbl.Col(nameof(PoAllBasicTypes.U16Val)).Type<ushort, ushort>("INTEGER");
		tbl.Col(nameof(PoAllBasicTypes.I16Val)).Type<short, short>("INTEGER");
		tbl.Col(nameof(PoAllBasicTypes.U32Val)).Type<uint, uint>("INTEGER");
		tbl.Col(nameof(PoAllBasicTypes.I32Val)).Type<int, int>("INTEGER");
		tbl.Col(nameof(PoAllBasicTypes.U64Val)).Type<ulong, ulong>("INTEGER");
		tbl.Col(nameof(PoAllBasicTypes.I64Val)).Type<long, long>("INTEGER");
		tbl.Col(nameof(PoAllBasicTypes.I32Nullable)).Type<int?, int?>("INTEGER");
		tbl.Col(nameof(PoAllBasicTypes.I64Nullable)).Type<long?, long?>("INTEGER");
		tbl.Col(nameof(PoAllBasicTypes.F32Val)).Type<float, float>("REAL");
		tbl.Col(nameof(PoAllBasicTypes.F64Val)).Type<double, double>("REAL");
		tbl.Col(nameof(PoAllBasicTypes.F64Nullable)).Type<double?, double?>("REAL");
		tbl.Col(nameof(PoAllBasicTypes.StrVal)).Type<string, string>("TEXT").NotNull();
		tbl.Col(nameof(PoAllBasicTypes.StrNullable)).Type<string?, string?>("TEXT");
		tbl.Col(nameof(PoAllBasicTypes.BlobVal)).Type<byte[], byte[]>("BLOB").NotNull();
		tbl.Col(nameof(PoAllBasicTypes.BlobNullable)).Type<byte[]?, byte[]?>("BLOB");
		mgr.AddTbl(tbl);

		// ===== Kv:帶軟刪,覆蓋基礎 CRUD/批量/軟刪 =====
		var tblKv = Table.FnSetTbl<TestKv>(mapper)("TestKv");
		CfgSoftDelTable(tblKv, IdTestKv.MkTypeMapFn());
		tblKv.Col(x=>x.Owner).MapType(IdTestUser.MkTypeMapFn());
		mgr.AddTbl(tblKv);

		// ===== 聚合:Word + Prop + Learn =====
		var tblWord = Table.FnSetTbl<TestWord>(mapper)("TestWord");
		CfgSoftDelTable(tblWord, IdTestWord.MkTypeMapFn());
		tblWord.Col(x=>x.Owner).MapType(IdTestUser.MkTypeMapFn());
		mgr.AddTbl(tblWord);

		var tblProp = Table.FnSetTbl<TestWordProp>(mapper)("TestWordProp");
		CfgSoftDelTable(tblProp, IdTestWordProp.MkTypeMapFn());
		tblProp.Col(x=>x.WordId).MapType(IdTestWord.MkTypeMapFn());
		mgr.AddTbl(tblProp);

		var tblLearn = Table.FnSetTbl<TestWordLearn>(mapper)("TestWordLearn");
		CfgSoftDelTable(tblLearn, IdTestWordLearn.MkTypeMapFn());
		tblLearn.Col(x=>x.WordId).MapType(IdTestWord.MkTypeMapFn());
		mgr.AddTbl(tblLearn);

		mgr.AddAgg(
			AggReg<TestJnWord, TestWord, IdTestWord>.Mk(
				tblWord.Tbl
				,x=>x.Id
				,(root, qry)=>new TestJnWord(
					root
					,qry.GetMany<TestWordProp, IdTestWord>(root.Id)
					,qry.GetMany<TestWordLearn, IdTestWord>(root.Id)
				)
			)
			.AddOneToMany(
				tblProp.Tbl
				,nameof(TestWordProp.WordId)
				,x=>x.WordId
			)
			.AddOneToMany(
				tblLearn.Tbl
				,nameof(TestWordLearn.WordId)
				,x=>x.WordId
			)
		);
		return mgr;
	}

	/// <summary>配置主鍵(強類型 Id 映射 BLOB)、軟刪列(DelAt:0=未刪,非0=已刪)。</summary>
	static void CfgSoftDelTable<TEntity, TId>(
		ITblSetter<TEntity> Setter
		,IUpperTypeMapFnT<u8[], TId> IdMapFn
	)
		where TEntity:class, new()
	{
		var o = Setter;
		o.Tbl.CodeIdName = nameof(TestKv.Id);
		o.Col(nameof(TestKv.Id))
			.MapType(IdMapFn)
			.NotNull()
			.AdditionalSqls(["PRIMARY KEY"]);

		var t = o.Tbl;
		t.SoftDelCol = new SoftDelol{
			CodeColName = nameof(TestKv.DelAt)
			,FnDelete = (old)=>{
				return 1L;
			}
			,FnRestore = (old)=>{
				return 0L;
			}
			,FnSqlIsDel = ()=>t.QtCol(nameof(TestKv.DelAt))+"<>0"
			,FnSqlIsNonDel = ()=>t.QtCol(nameof(TestKv.DelAt))+"=0"
		};
	}
}
