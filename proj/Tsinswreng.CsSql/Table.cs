#define Impl
namespace Tsinswreng.CsSql;

using System.Linq.Expressions;
using Tsinswreng.CsCore;
using Tsinswreng.CsPage;
using Tsinswreng.Srefl;
using Tsinswreng.CsTools;
using IStr_Any = System.Collections.Generic.IDictionary<str, obj?>;
using Str_Any = System.Collections.Generic.Dictionary<str, obj?>;


public partial class Table<T>: Table, ITable<T>{

}


public partial class Table:ITable{
	[Doc(@$"")]
	public ITblMgr TblMgr{get;set;} = null!;
	public IDbStuff DbStuff => TblMgr.DbStuff;

	public IPropAccessorMgr PropAccessorMgr{get;set;}

	public Type CodeEntityType{get;set;}

	#pragma warning disable CS8618
	public Table(){}

	public Table(
		IPropAccessorMgr PropAccessorMgr
		,str Name
		,IDictionary<str, Type> CodeCol_UpperType
	){
		this.PropAccessorMgr = PropAccessorMgr;
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


	public static IDictionary<str, Type> GetTypeDictByAccessor(
		IPropAccessorMgr PropAccessorMgr
		,Type EntityClrType
	){
		if(!PropAccessorMgr.Type_PropAccessor.TryGetValue(EntityClrType, out var Accessor)){
			throw new Exception($"No {nameof(IPropAccessor)} registered for entity type: {EntityClrType}");
		}
		var Ans = new Dictionary<str, Type>();
		foreach(var Key in Accessor.GetGetterNames(null)){
			if(!Accessor.TryGetType(Key, out var Type) || Type is null){
				continue;
			}
			Ans[Key] = Type;
		}
		return Ans;
	}

	public static ITable<TEntity> Mk<TEntity>(
		IPropAccessorMgr PropAccessorMgr
		,str DbTblName
		,IDictionary<str, Type> Key_Type
	){
		return Mk<TEntity>(typeof(TEntity), PropAccessorMgr, DbTblName, Key_Type);
	}

	public static ITable<TEntity> Mk<TEntity>(
		IPropAccessorMgr PropAccessorMgr
		,str DbTblName
	){
		var EntityClrType = typeof(TEntity);
		var Key_Type = GetTypeDictByAccessor(PropAccessorMgr, EntityClrType);
		return Mk<TEntity>(EntityClrType, PropAccessorMgr, DbTblName, Key_Type);
	}


	public static ITable<TEntity> Mk<TEntity>(
		Type EntityClrType
		,IPropAccessorMgr PropAccessorMgr
		,str DbTblName
		,IDictionary<str, Type> Key_Type
	){
		var t = new Table<TEntity>{
			PropAccessorMgr = PropAccessorMgr
			,DbTblName = DbTblName
			,CodeCol_UpperType = Key_Type
			,CodeEntityType = EntityClrType
		};
		t.Init();
		return t;
	}
	
	

	[Obsolete("")]
		public static Func<str, ITable<T>> FnMkTbl<T>(IPropAccessorMgr PropAccessorMgr){
 		ITable<T2> Mk<T2>(str DbTblName){
				var TypeDict = GetTypeDictByAccessor(PropAccessorMgr, typeof(T2));
			return Table.Mk<T2>(
					PropAccessorMgr
				,DbTblName
				,TypeDict
			);
		}
		return Mk<T>;
	}
	
		public static Func<str, ITblSetter<T>> FnSetTbl<T>(IPropAccessorMgr PropAccessorMgr){
		ITblSetter<T2> Set<T2>(str DbTblName){
				var TypeDict = GetTypeDictByAccessor(PropAccessorMgr, typeof(T2));
			var Tbl = Table.Mk<T2>(
					PropAccessorMgr
				,DbTblName
				,TypeDict
			);
			var R = new TblSetter<T2>(Tbl);
			return R;
		}
		return Set<T>;
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
	public ISqlMkr SqlMkr => DbStuff.SqlMkr;

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
