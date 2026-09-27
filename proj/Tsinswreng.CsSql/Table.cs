#define Impl
namespace Tsinswreng.CsSql;

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Tsinswreng.CsCore;
using Tsinswreng.CsPage;
using Tsinswreng.CsRefl;
using Tsinswreng.CsTools;
using IStr_Any = System.Collections.Generic.IDictionary<str, obj?>;
using Str_Any = System.Collections.Generic.Dictionary<str, obj?>;


public partial class Table<T>: Table, ITable<T>{

}


public partial class Table:ITable{
	[Doc(@$"")]
	public ITblMgr TblMgr{get;set;} = null!;
	public IDbStuff DbStuff => TblMgr.DbStuff;

	[Doc($@"型別元資料來源；實體成員的按名讀寫都經它")]
	public ITypeInfoSrc TypeInfoSrc{get;set;}

	public Type CodeEntityType{get;set;}

	#pragma warning disable CS8618
	public Table(){}

	/// 建表：給型別元資料來源、表名，與「代碼列名 → 上層型別」的表。
	public Table(
		ITypeInfoSrc TypeInfoSrc
		,str Name
		,IDictionary<str, Type> CodeCol_UpperType
	){
		this.TypeInfoSrc = TypeInfoSrc;
		this.DbTblName = Name;
		this.CodeCol_UpperType = CodeCol_UpperType;
	}

	bool _Inited = false;
	public ITable Init(){
		if(_Inited){
			return this;
		}
		foreach(var (Key,Type) in CodeCol_UpperType){
			var Col = new Column();
			Col.DbName = Key;
			Columns[Key] = Col;
			DbColName_CodeColName[Key] = Key;
			Col.RawClrType = Type;
			Col.UpperClrType = Type;
			// if(v != null){
			// 	Col.RawClrType = v.GetType();
			// 	Col.UpperClrType = v.GetType();
			// }

		}
		_Inited = true;
		return this;
	}


	[Doc($@"
	#Sum[由型別元資料算出「代碼列名 → 上層型別」的表，供建表時初始化列用。]
	#Params([TypeInfoSrc, 型別元資料來源], [EntityClrType, 實體型別])
	#Rtn[代碼列名 → 上層型別；順序同該型別的成員序]
	#Descr[
	只收可讀成員：值要能從實體取出來，才談得上當一列。
	型別不在來源裏時拋 Exception。
	]
	")]
	public static IDictionary<str, Type> GetCodeCol_UpperType(
		ITypeInfoSrc TypeInfoSrc
		// 本方法把型別交給 CsRefl 的來源，故要求調用方保證該型別的成員元資料會被保留。
		// 少了這個註解，NativeAOT 剪裁後 Info.ReadableMembers 是空的，表也就一列都沒有。
		,[DAM(ReflTypeInfo.ReflDam)] Type EntityClrType
	){
		if(!TypeInfoSrc.TryGetInfo(EntityClrType, out var Info) || Info is null){
			throw new Exception($"No {nameof(ITypeInfo)} for entity type: {EntityClrType}");
		}
		var Ans = new Dictionary<str, Type>();
		foreach(var Key in Info.ReadableMembers.Keys){
			if(!Info.TryGetMember(Key, out var M)){
				continue;
			}
			Ans[Key] = M.PropertyType;
		}
		return Ans;
	}

	public static ITable<TEntity> Mk<TEntity>(
		ITypeInfoSrc TypeInfoSrc
		,str DbTblName
		,IDictionary<str, Type> Key_Type
	){
		return Mk<TEntity>(typeof(TEntity), TypeInfoSrc, DbTblName, Key_Type);
	}

	// TEntity 上的 DAM 是為了滿足 GetCodeCol_UpperType 的要求：呼叫方傳具體型別時要求就在那裏滿足。
	public static ITable<TEntity> Mk<[DAM(ReflTypeInfo.ReflDam)] TEntity>(
		ITypeInfoSrc TypeInfoSrc
		,str DbTblName
	){
		var EntityClrType = typeof(TEntity);
		var Key_Type = GetCodeCol_UpperType(TypeInfoSrc, EntityClrType);
		return Mk<TEntity>(EntityClrType, TypeInfoSrc, DbTblName, Key_Type);
	}


	public static ITable<TEntity> Mk<TEntity>(
		Type EntityClrType
		,ITypeInfoSrc TypeInfoSrc
		,str DbTblName
		,IDictionary<str, Type> Key_Type
	){
		var t = new Table<TEntity>{
			TypeInfoSrc = TypeInfoSrc
			,DbTblName = DbTblName
			,CodeCol_UpperType = Key_Type
			,CodeEntityType = EntityClrType
		};
		t.Init();
		return t;
	}
	
	

	[Obsolete("")]
		public static Func<str, ITable<T>> FnMkTbl<[DAM(ReflTypeInfo.ReflDam)] T>(ITypeInfoSrc TypeInfoSrc){
		// 原本這裏是一個 local function；local function 的型別參數不能標特性，
		// 故抽成下面的靜態方法，好把 DAM 要求標上，讓 typeof(T) 的元資料在 AOT 下不被剪掉。
		return DbTblName => MkTblCore<T>(TypeInfoSrc, DbTblName);
	}

	static ITable<T> MkTblCore<[DAM(ReflTypeInfo.ReflDam)] T>(ITypeInfoSrc TypeInfoSrc, str DbTblName){
		var TypeDict = GetCodeCol_UpperType(TypeInfoSrc, typeof(T));
		return Table.Mk<T>(
				TypeInfoSrc
			,DbTblName
			,TypeDict
		);
	}
	
		public static Func<str, ITblSetter<T>> FnSetTbl<[DAM(ReflTypeInfo.ReflDam)] T>(ITypeInfoSrc TypeInfoSrc){
		// 同上：抽成靜態方法才標得上 DAM。
		return DbTblName => MkTblSetterCore<T>(TypeInfoSrc, DbTblName);
	}

	static ITblSetter<T> MkTblSetterCore<[DAM(ReflTypeInfo.ReflDam)] T>(ITypeInfoSrc TypeInfoSrc, str DbTblName){
		var TypeDict = GetCodeCol_UpperType(TypeInfoSrc, typeof(T));
		var Tbl = Table.Mk<T>(
				TypeInfoSrc
			,DbTblName
			,TypeDict
		);
		return new TblSetter<T>(Tbl);
	}

	[Impl]
	public str DbTblName{get;set;}
	#if Impl
	= "";
	#endif

	[Impl]

	public IDictionary<str, IColumn> Columns{get;set;}
	#if Impl
	= new Dictionary<str, IColumn>();
	#endif

	[Impl]
	public str CodeIdName{get;set;}
	#if Impl
	= "Id";
	#endif

	[Impl]
	public ISoftDeleteCol? SoftDelCol{get;set;}

	[Impl]
	public IDictionary<str, str> DbColName_CodeColName{get;set;}
	#if Impl
	= new Dictionary<str, str>();
	#endif

	[Impl]
	public IDictionary<str, Type> CodeCol_UpperType{get;set;}
	#if Impl
	= new Dictionary<str, Type>();
	#endif

	[Impl]
	public ISqlDialect SqlDialect => DbStuff.SqlDialect;

	[Impl]
	public IList<str> InnerAdditionalSqls{get;set;}
#if Impl
	= new List<str>();
#endif

	[Impl]
	public IList<str> OuterAdditionalSqls{get;set;}
#if Impl
	= new List<str>();
#endif

	public IDictionary<Type, IUpperTypeMapFn> UpperType_DfltMapper{get;set;}
#if Impl
	= new Dictionary<Type, IUpperTypeMapFn>();
#endif
}

#pragma warning disable CS8601
