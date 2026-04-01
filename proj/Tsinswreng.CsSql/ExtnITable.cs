namespace Tsinswreng.CsSql;

using System.Linq.Expressions;
using Tsinswreng.CsCore;
using Tsinswreng.CsPage;
using Tsinswreng.Srefl;
using Tsinswreng.CsTools;
using IStr_Any = System.Collections.Generic.IDictionary<str, obj?>;
using Str_Any = System.Collections.Generic.Dictionary<str, obj?>;
public static class ExtnITable {
	extension(ITable z) {
		[Doc($@"
		Quote field name for SQL, e.g. in sqlite: ""field_name""; in mysql: `field_name`;
		")]
		public str Qt(
			str s
		) {
			return z.SqlMkr.Quote(s);
		}

		/// 宜用此㕥取列 無旹自有報錯ʹ訊
		[Doc(@$" You Should use this instead of directly reading from {nameof(ITable.Columns)}
		#Example[ If you have an entity class named `PoUser` which has `UserName` field,
		use fn(nameof(PoUser.UserName)) to get the {nameof(IColumn)} object of the `UserName` field.
		]
		")]
		public IColumn GetCol(str CodeColName) {
			if (!z.Columns.TryGetValue(CodeColName, out var Col)) {
				throw new Exception($"In Table {z.DbTblName}, No such column: {CodeColName}\nAvailable columns: {string.Join(", ", z.Columns.Keys)}");
			}
			return Col;
		}


		[Doc(@$"#Sum[Map entity field name to database column name.]
		#Params([Column name C# code (entity field)])
		#Rtn[Column name in database table]
		")]
		public str DbCol(
			str CodeColName
		) {
			var Col = z.GetCol(CodeColName);
			var DbColName = Col.DbName;
			return DbColName;
		}

		[Doc(@$"#Sum[Map entity field name to quoted database column name.]
		#See[{nameof(DbCol)} then {nameof(ExtnITable.Qt)}]
		")]
		public str QtCol(
			str CodeColName
		) {
			var dbColName = z.DbCol(CodeColName);
			return z.SqlMkr.Quote(dbColName);
		}

		[Doc(@$"
		#Example[fn<PoUser>(x=>x.UserName) returns string UserName]
		")]
		[Doc($@"
		#Sum[Get member name from expression]
		#Params([Expression to extract member name from])
		#Rtn[Member name as string]
		#Example[fn<PoUser>(x=>x.UserName) returns string ""UserName""]
		")]
		public str Memb<T>(Expression<Func<T, obj?>> ExprMemb) {
			return ToolExpr.GetMemberName(ExprMemb);
		}

		[Doc($@"
		#Sum[Create field reference from expression]
		#Params([Expression to extract member name from])
		#Rtn[IField object with table context]
		")]
		public IDbField QtCol<T>(Expression<Func<T, obj?>> ExprMemb) {
			var memberName = ToolExpr.GetMemberName<T>(ExprMemb);
			var R = new Field(z, memberName);
			return R;
		}
		
		public obj? GetEntityId<T>(T Entity){
			if(z.PropAccessorReg.TryGet(Entity, z.CodeIdName, out var Id)){
				return Id;
			}
			throw new Exception($"{typeof(T)} does not have property {z.CodeIdName}");
		}

		[Doc($@"
		#Sum[Create typed SQL splicer]
		#Rtn[Typed SQL splicer with table context set]
		")]
		public ISqlSplicer<T> SqlSplicer<T>() {
			var R = new SqlSplicer<T>();
			R.Tbl = z;
			return R;
		}

		[Doc($@"
		#Sum[Create untyped SQL splicer]
		#Rtn[Untyped SQL splicer with table context set]
		")]
		public ISqlSplicer SqlSplicer() {
			var R = new SqlSplicer();
			R.Tbl = z;
			return R;
		}


		[Doc($@"
		#Sum[Map code column name to quoted database column name]
		#Params([IParam whose Name is the code column name])
		#Rtn[Quoted database column name]
		")]
		public str QtCol(
			IParam CodeColNameParam
		) {
			var CodeColName = CodeColNameParam.Name;
			var DbColName = z.DbCol(CodeColName);
			return z.SqlMkr.Quote(DbColName);
		}

		[Doc($@"
		#Sum[Create SQL parameter with specified name]
		#Params([Parameter name])
		")]
		public IParam Prm(
			str Name
		) {
			return z.SqlMkr.Param(Name);
		}

		[Doc($@"
		#Sum[Create SQL parameter with auto-generated unique name]
		#Rtn[IParam with name like ""@_1ccGi7C87H-LETKfaB_JX""]
		")]
		public IParam Prm() {
			var bytes = Ulid.NewUlid().ToByteArray();
			var id = ToolUInt128.ByteArrToUInt128(bytes);
			var Name = ToolUInt128.ToLow64Base(id);
			return z.SqlMkr.Param("_" + Name);
		}

		[Doc($@"
		#Sum[Convert database dictionary to code dictionary]
		#Params([Dictionary with database column names as keys])
		#Rtn[Dictionary with code column names as keys and upper-cased values]
		")]
		public IStr_Any ToCodeDict(
			IStr_Any DbDict
		) {
			var ans = new Str_Any();
			foreach (var (kDb, vDb) in DbDict) {
				//TODO 製函數㕥統一字典取值、取不到則友好ᵈ報錯並列有效ʹ鍵
				var kCode = z.DbColName_CodeColName[kDb];
				var colCode = z.Columns[kCode];
				var vCode = colCode.RawToUpper?.Invoke(vDb) ?? vDb;
				ans[kCode] = vCode;
			}
			return ans;
		}

		[Doc($@"
		#Sum[Assign database values to existing entity]
		#Params([Dictionary with database column names], [Entity to assign values to])
		#Rtn[The assigned entity]
		")]
		public T AssignEntity<T>(
			IStr_Any DbDict
			, T ToBeAssigned
		) {
			var CodeDict = z.ToCodeDict(DbDict);
			z.AssignEntityByCodeDict(typeof(T), ToBeAssigned!, CodeDict);
			return ToBeAssigned;
		}

		[Doc($@"
		#Sum[Convert database dictionary to entity with reference]
		#Params([Dictionary with database column names], [Reference to entity instance, will be created if null])
		#Rtn[The entity instance]
		")]
		public TPo DbDictToEntity<TPo>(
			IStr_Any DbDict
			, ref TPo R
		) where TPo : new() {
			var CodeDict = z.ToCodeDict(DbDict);
			R ??= new TPo();
			z.AssignEntityByCodeDict(typeof(TPo), R!, CodeDict);
			return R;
		}

		[Doc($@"
		#Sum[Assign code dictionary to entity via string accessor]
		#Params([Entity clr type], [Entity instance], [Dictionary with code field names as keys])
		")]
		public obj AssignEntityByCodeDict(
			Type EntityType
			,obj Entity
			,IStr_Any CodeDict
		) {
			if (!z.PropAccessorReg.Type_PropAccessor.TryGetValue(EntityType, out var accessor)) {
				throw new Exception($"No {nameof(IPropAccessor)} registered for type: {EntityType}");
			}
			foreach (var (kCode, vCode) in CodeDict) {
				if (!accessor.TrySet(Entity, kCode, vCode)) {
					continue;
				}
			}
			return Entity;
		}

		[Doc($@"
		#Sum[Convert entity to code dictionary via string accessor]
		#Params([Entity instance], [Optional entity clr type])
		#Rtn[Dictionary with code field names as keys]
		")]
		public IStr_Any EntityToCodeDict(
			obj Entity
			,Type? EntityType = null
		) {
			EntityType ??= Entity.GetType();
			if (!z.PropAccessorReg.Type_PropAccessor.TryGetValue(EntityType, out var accessor)) {
				throw new Exception($"No {nameof(IPropAccessor)} registered for type: {EntityType}");
			}
			var ans = new Str_Any();
			foreach (var key in accessor.GetGetterNames(Entity)) {
				if (!accessor.TryGet(Entity, key, out var vCode)) {
					continue;
				}
				ans[key] = vCode;
			}
			return ans;
		}

		[Doc($@"
		#Sum[Convert database dictionary to new entity]
		#Params([Dictionary with database column names])
		#Rtn[New entity instance with assigned values]
		")]
		public TPo DbDictToEntity<TPo>(
			IStr_Any DbDict
		) where TPo : new() {
			var R = new TPo();
			z.DbDictToEntity(DbDict, ref R);
			return R;
		}

		//static i32 i = 0;

		[Doc($@"
		#Sum[Convert code dictionary to database dictionary]
		#Params([Dictionary with code column names as keys])
		#Rtn[Dictionary with database column names as keys and raw values]
		")]
		public IStr_Any ToDbDict(
			IStr_Any CodeDict
		) {
			var ans = new Str_Any();
			foreach (var (kCode, vCode) in CodeDict) {
				var Col = z.Columns[kCode];
				var vDb = Col.UpperToRaw?.Invoke(vCode) ?? vCode;
				ans[Col.DbName] = vDb;
			}
			return ans;
		}

		[Doc($@"
		#Sum[Convert upper value to raw value for specific column]
		#Params([Upper value], [Code column name])
		")]
		public obj? UpperToRaw(
			obj? UpperValue
			, str CodeColName
		) {
			var Col = z.GetCol(CodeColName);
			return Col.UpperToRaw?.Invoke(UpperValue) ?? UpperValue;
		}

		[Doc($@"
		#Sum[Convert upper value to raw value with type fallback]
		#Params([Upper value], [Upper type], [Optional code column name for column-specific conversion])
		#Rtn[Raw value after conversion, or original value if no converter found]
		")]
		public obj? UpperToRaw(
			obj? UpperValue
			, Type UpperType
			, str? CodeColName = null
		) {
			if (CodeColName != null
				&& (z.Columns.TryGetValue(CodeColName, out var Col))
			) {
				return Col.UpperToRaw?.Invoke(UpperValue) ?? UpperValue;
			}
			if (z.UpperType_DfltMapper.TryGetValue(UpperType, out var Mapper)) {
				return Mapper.UpperToRaw?.Invoke(UpperValue) ?? UpperValue;
			}
			return UpperValue;
		}


		[Doc($@"
		#Sum[Generic version of UpperToRaw with type inference]
		#Params([Upper value], [Optional code column name])
		")]
		public obj? UpperToRaw<T>(
			T UpperValue
			, str? CodeColName = null
		) {
			return z.UpperToRaw(UpperValue, typeof(T), CodeColName);
		}

		[Doc($@"
		#Sum[Convert raw value to upper value for specific column]
		#Params([Raw value from database], [Code column name])
		")]
		public obj? RawToUpper(
			obj? RawValue
			, str CodeColName
		) {
			var Col = z.GetCol(CodeColName);
			return Col.RawToUpper?.Invoke(RawValue) ?? RawValue;
		}

		// public T RawToUpper<T>(//此處T非實體類 洏是列ʹ類型
		// 	obj? RawValue
		// 	,Expression<Func<T,obj?>> ExprMemb
		// ){
		// 	var CodeColName = ToolExpr.GetMemberName(ExprMemb);
		// 	return z.RawToUpper<T>(RawValue, CodeColName);
		// }


		[Doc($@"
		#Sum[Convert raw value to upper value with generic return type]
		#Params([Raw value from database], [Code column name])
		#Rtn[Converted value cast to T]
		")]
		public T RawToUpper<T>(
			obj? RawValue
			, str CodeColName
		) {
			var Col = z.GetCol(CodeColName);
			return (T)(Col.RawToUpper?.Invoke(RawValue) ?? RawValue)!;
		}

		[Doc($@"
		#Sum[Generate UPDATE clause for specified fields]
		#Params([Enumerable of code field names to update])
		#Rtn[SQL SET clause like ""field1 = @field1, field2 = @field2""]
		")]
		public str UpdateClause(
			IEnumerable<str> UpperFields
		) {
			List<str> segs = [];
			foreach (var rawField in UpperFields) {
				var field = z.QtCol(rawField);
				var param = z.Prm(rawField);
				segs.Add(field + " = " + param);
			}
			return string.Join(", ", segs);
		}

		[Doc($@"
		#Sum[Generate INSERT clause for specified fields]
		#Params([Enumerable of code field names to insert])
		#Rtn[SQL INSERT clause like ""(field1, field2) VALUES (@field1, @field2)""]
		")]
		public str InsertClause(
			IEnumerable<str> RawFields
		) {
			List<str> Fields = [];
			List<IParam> Params = [];
			foreach (var rawField in RawFields) {
				var field = z.QtCol(rawField);
				var param = z.Prm(rawField);
				Fields.Add(field);
				Params.Add(param);
			}
			return "(" + string.Join(", ", Fields) + ") VALUES (" + string.Join(", ", Params) + ")";
		}

		[Doc($@"
		#Sum[Generate INSERT clause for batch operations with numbered parameters]
		#Params([Enumerable of code field names], [Number of parameter groups])
		#Rtn[SQL INSERT clause with multiple value groups like ""(field1, field2) VALUES (@field1__0, @field2__0), (@field1__1, @field2__1)...""]
		")]
		public str InsertManyClause(
			IEnumerable<str> RawFields
			, u64 GroupCnt = 1000
		) {
			IList<str> Fields = [];
			foreach (var rawField in RawFields) {
				var field = z.QtCol(rawField);
				var param = z.Prm(rawField);
				Fields.Add(field);
			}


			IList<IList<IParam>> ParamLists = [];
			for (u64 i = 0; i < GroupCnt; i++) {
				IList<IParam> Params = [];
				foreach (var rawField in RawFields) {
					var param = z.NumFieldParam(rawField, i);
					Params.Add(param);
				}
				ParamLists.Add(Params);
			}
			var sqlFieldsValus = "(" + string.Join(", ", Fields) + ") VALUES";

			List<str> R = [];
			for (u64 i = 0; i < GroupCnt; i++) {
				var ParamList = ParamLists[i.AsI32()];
				R.Add(" (" + string.Join(", ", ParamList) + ")");
			}

			return sqlFieldsValus + " " + string.Join(", ", R);
		}

		[Doc(@$"
		Usually used in batch operation, the number affix is used to mark its batch number and avoid duplication param name
		#See[{nameof(IParam.NumSuffixName)}]
		#See[{nameof(IParam)}.this[u64]]
		")]
		public IParam NumFieldParam(
			str Field, u64 Num
		) {
			return z.Prm(Field + "__" + Num);
		}

		/// Num:0 -> @_0
		[Doc($@"
		#Sum[Create numbered parameter]
		#Params([Number to use as suffix])
		#Rtn[IParam with name like ""@_0"", ""@_1""]
		")]
		public IParam NumParam(
			u64 Num
		) {
			return z.Prm("_" + Num);
		}


		[Doc($@"
		#Sum[Generate numbered parameters from StartPos to EndPos inclusive]
		#Params([End position], [Start position, default 0])
		#Rtn[List of IParam from @_StartPos to @_EndPos]
		")]
		public IList<IParam> NumParamsEndStart(
			u64 EndPos, u64 StartPos = 0
		) {
			var R = new List<IParam>();
			for (u64 i = StartPos; i <= EndPos; i++) {
				var Param = z.NumParam(i);
				R.Add(Param);
			}
			return R;
		}

		[Doc($@"
		#Sum[Generate numbered parameters from StartPos to EndPos inclusive]
		#Params([Start position], [End position])
		#Rtn[List of IParam from @_StartPos to @_EndPos]
		")]
		public IList<IParam> NumParams(
			u64 StartPos, u64 EndPos
		) {
			var R = new List<IParam>();
			for (u64 i = StartPos; i <= EndPos; i++) {
				var Param = z.NumParam(i);
				R.Add(Param);
			}
			return R;
		}

		[Doc($@"
		#Sum[Generate numbered parameters from 0 to Cnt-1]
		#Params([Count of parameters])
		#Rtn[List of IParam from @_0 to @_{{Cnt - 1}}]
		#Example[fn(2) -> [@_0, @_1]]
		")]
		public IList<IParam> NumParams(
			u64 Cnt
		) {
			return z.NumParams(0, Cnt - 1);
		}


		[Doc($@"
		#Sum[Generate numbered parameter clause for IN clause]
		#Params([End position], [Start position, default 0])
		#Rtn[SQL clause like ""(@0, @1, @2)""]
		")]
		public str NumParamClause(
			u64 EndPos
			, u64 StartPos = 0
		) {
			List<str> R = [];
			R.Add("(");
			for (u64 i = StartPos; i <= EndPos; i++) {
				var Param = z.Prm(i + "").ToString() ?? "";
				R.Add(Param);
				if (i == EndPos) {
					R.Add(", ");
				}
			}
			R.Add(")");
			return string.Join("", R);
		}

		[Doc($@"
		#Sum[Generate numbered parameters from Start to End inclusive]
		#Params([Start number], [End number])
		#Rtn[List of IParam from @Start to @End]
		")]
		public IList<IParam> Prm(
			u64 Start
			, u64 End
		) {
			var R = new List<IParam>();
			for (u64 i = Start; i <= End; i++) {
				var Param = z.Prm(i + "");
				R.Add(Param);
			}
			return R;
		}

		[Doc($@"
		#Sum[Generate equality condition]
		#Params([Database column name], [Parameter])
		#Rtn[SQL equality like ""DbColName = @Param""]
		")]
		public str Eq(
			str DbColName, IParam Param
		) {
			return z.SqlMkr.Eq(DbColName, Param);
		}

		[Doc($@"
		#Sum[Generate self-equality condition]
		#Params([Parameter whose Name is used as column name])
		#Rtn[SQL equality like ""Param.Name = Param""]
		")]
		public str Eq(IParam Param) {
			return z.Eq(Param.Name, Param);
		}

		[Doc($@"
		#Sum[Append SQL statements to {nameof(ITable.OuterAdditionalSqls)}]
		#Rtn[Same table instance for chaining]
		")]
		public ITable AddOuterSql(params str[] Sqls) {
			foreach (var sql in Sqls) {
				if (!str.IsNullOrWhiteSpace(sql)) {
					z.OuterAdditionalSqls.Add(sql);
				}
			}
			return z;
		}
		
		[Doc(@$"generate sql for create table")]
		public str SqlMkTbl() {
			str OneCol(ITable Tbl, IColumn Col) {
				var R = new List<str>();
				R.Add(Tbl.Qt(Col.DbName));
				if (!str.IsNullOrEmpty(Col.DbType)) {
					R.Add(Col.DbType);
				}
				else {
					if (Col.RawClrType == null) {
						var Msg = $"{Col.DbName}:\nCol.RawClrType == null";
						throw new Exception("Col.RawClrType == null");
					}
					try {
						var DbTypeName = z.SqlMkr.SqlTypeMapper.ToDbTypeName(Col.RawClrType);
						R.Add(DbTypeName);
					}
					catch (System.Exception e) {
						throw new Exception($"Type Mapping Error:\n{Tbl.DbTblName}.{Col.DbName}\n{e}");
					}
				}
				R.AddRange(Col.AdditionalSqls ?? []);
				if (Col.NotNull) {
					R.Add("NOT NULL");
				}
				return string.Join(" ", R);
			}

			str FmtInnerSqls(IList<str>? Sqls) {
				if (Sqls == null || Sqls.Count == 0) {
					return "";
				}
				return $",\n\n{str.Join(",\n\t", z.InnerAdditionalSqls ?? [])}";
			}

			str FmtOuterSqls(IList<str>? Sqls) {
				if (Sqls == null || Sqls.Count == 0) {
					return "";
				}
				List<str> R = [];
				foreach (var sql in Sqls) {
					R.Add(sql);
					R.Add(";\n");
				}
				return str.Join("", R);
			}

			var Lines = new List<str>();
			foreach (var (name, Col) in z.Columns) {
				Lines.Add(OneCol(z, Col));
			}
			var S =
			$"""
	CREATE TABLE IF NOT EXISTS {z.Qt(z.DbTblName)}(
		{string.Join(",\n\t", Lines)}{FmtInnerSqls(z.InnerAdditionalSqls)}
	);
	{FmtOuterSqls(z.OuterAdditionalSqls)}
	""";
			return S;
		}


		[Doc($@"
		#Sum[Create page query that selects all records]
		#Desc[Sets PageSize to MaxValue, not a real SELECT ALL]
		")]
		public IPageQry PageSlctAll() {
			var R = new PageQry();
			if (z.TblMgr?.DbSrcType == EDbSrcType.Postgres) {
				R.PageSize = i64.MaxValue;
			}
			else {
				R.PageSize = u64.MaxValue;
			}
			return R;
		}
		
		
		[Doc(@$"Expr that filter non deleted rows
		you do not need to add 'AND' before it
		if {nameof(z.SoftDelCol)} is not set, it will return empty str
		#See[{nameof(z.SoftDelCol.FnSqlIsNonDel)}]
		")]
		public str AndSqlIsNonDel(){
			if(z.SoftDelCol is null){
				return "";
			}
			return "AND " + z.SoftDelCol.FnSqlIsNonDel()??"";
		}
		
	}

}

#if DEBUG
public class Dbg {
	public static i64 Cnt = 0;
}
#endif
