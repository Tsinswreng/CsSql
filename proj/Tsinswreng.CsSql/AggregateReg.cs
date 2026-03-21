namespace Tsinswreng.CsSql;

file class __FileDoc{
	str Doc = @"""
#Sum[
AggregateReg.cs 相关聚合注册与上下文类型定义
]
#Descr[
本文件定义了聚合根、聚合关系、聚合装配上下文等相关类型，详见各类型注释。
]
""";
}


[Doc("""
#Sum[
Defines the relationship kind between a root aggregate entity and included entities.
]
#Descr[
Used to distinguish how related entities should be loaded and assembled.
Example: A Word (root) can have multiple WordProps (OneToMany) or a single User (OneToOne)
]
""")]
public enum EAggRelKind {
	[Doc(@"#Sum[One-to-one relationship: Each root entity has exactly one related entity]")]
	OneToOne = 1,
	[Doc(@"#Sum[One-to-many relationship: Each root entity can have zero or more related entities]")]
	OneToMany = 2,
}

[Doc("""
#Sum[
Context for building/assembling aggregates during data retrieval.
]
#Descr[
Acts as a temporary cache that holds related entities grouped by type and key,
enabling the assembly phase to fetch pre-loaded related data.
Example: When loading a Word aggregate with its Props and Learn records,
the context stores lists of Props and Learn records keyed by WordId,
so the FnAssemble function can retrieve them without additional queries.
]
""")]
public partial interface IAggQryCtx {
	[Doc(@"""
#Sum[Add a related entity to the context under its type and key]
#Params(
	[The CLR type of the entity (not root entity) , e.g., typeof(PoWordProp) ],
	[The grouping key e.g., WordId value],
	[The entity instance to add],
)
""")]
	public nil Add(Type SubEntityType, obj Key, obj SubEntity);

	[Doc(@"""
#Sum[Retrieve all related entities of a given type and key]
#Rtn[A list of entities matching the type and key; empty list if not found]
""")]
	public IList<obj> GetMany(Type EntityType, obj Key);

	[Doc(@"""
#Sum[Retrieve the first related entity of a given type and key]
#Rtn[The first entity, or null if not found]
""")]
	public obj? GetOne(Type EntityType, obj Key);
}

[Doc(@"""
#Sum[Default implementation of IAggBuildCtx that stores entities in nested dictionaries.]
#Descr[The structure is: Dictionary[EntityType] -> Dictionary[Key] -> List[Entities]]
""")]
public partial class AggQryCtx
	: IAggQryCtx {
	[Doc(@"""
#Sum[Three-level storage structure for aggregated entities.]
#Descr[
Level 1: EntityType (e.g., typeof(PoWordProp))
Level 2: Key value (e.g., WordId: 123)
Level 3: List of all entities matching that type and key
]
""")]
	public IDictionary<Type, IDictionary<obj, IList<obj>>> EntityType_Key_Items { get; set; }
		= new Dictionary<Type, IDictionary<obj, IList<obj>>>();

	public nil Add(Type EntityType, obj Key, obj Entity) {
		if (!EntityType_Key_Items.TryGetValue(EntityType, out var key_Items)) {
			key_Items = new Dictionary<obj, IList<obj>>();
			EntityType_Key_Items[EntityType] = key_Items;
		}
		if (!key_Items.TryGetValue(Key, out var items)) {
			items = [];
			key_Items[Key] = items;
		}
		items.Add(Entity);
		return NIL;
	}

	public IList<obj> GetMany(Type EntityType, obj Key) {
		if (!EntityType_Key_Items.TryGetValue(EntityType, out var key_Items)) {
			return [];
		}
		if (!key_Items.TryGetValue(Key, out var items)) {
			return [];
		}
		return items;
	}

	public obj? GetOne(Type EntityType, obj Key) {
		return GetMany(EntityType, Key).FirstOrDefault();
	}
}

[Doc(@"""
#Sum[Strongly-typed extension methods for IAggBuildCtx that avoid boxing/unboxing.]
#Descr[These provide generic versions of GetMany and GetOne. Example: Instead of ctx.GetOne(typeof(User), userId) and casting, use ctx.GetOne<User, IdUser>(userId) for type safety.]
""")]
public static partial class ExtnAggQryCtx {
	extension(IAggQryCtx z) {
		[Doc(@"""
	#Sum[Retrieve all related entities of type TPo with key TKey.]
	#TParams([The persistent obj type to retrieve],[The key type used for grouping])
	#Params([The key value to lookup (e.g., WordId: 123)])
	#Rtn[List of TPo instances; empty if not found. Never null.]
	""")]
		public IList<TPo> GetMany<TPo, TKey>(TKey Key) {
			var items = z.GetMany(typeof(TPo), Key!);
			return items.Select(x => (TPo)x).ToList();
		}

		[Doc(@"""
	#Sum[Retrieve the first related entity of type TPo with key TKey. Useful for one-to-one relationships.]
	#TParams([The persistent obj type to retrieve],[The key type used for grouping])
	#Params([The key value to lookup])
	#Rtn[The first TPo instance or null if not found]
	""")]
		public TPo? GetOne<TPo, TKey>(TKey Key) {
			var one = z.GetOne(typeof(TPo), Key!);
			if (one is null) {
				return default;
			}
			return (TPo)one;
		}
	}
}

[Doc(@"""
#Sum[Metadata about a single *related entity* (not Root Entity) set in an aggregate.]
#Descr[Stores information about how to load and retrieve related entities of a specific type. Example: An aggregate may have multiple includes: - PoWordProp items joined on WordId (OneToMany) - PoWordLearn items joined on WordId (OneToMany) - PoWordMeaning item joined on WordId (OneToOne)]
""")]
public partial interface IAggIncludeReg {
	[Doc(@"#Sum[The CLR type of the related entity (e.g., typeof(PoWordProp))]")]
	public Type EntityType { get; }

	[Doc(@"#Sum[AOT-safe factory function to create related entity instances]")]
	public Func<obj> FnNewEntityObj { get; }

	[Doc("""
#Sum[The column name in the related table used as the join key.]
#Descr[Example: "WordId" in the WordProp table]
""")]
	public str FKeyCodeCol { get; }

	[Doc(@"#Sum[The table definition for the related entity type]")]
	public ITable Tbl { get; }

	[Doc(@"#Sum[Whether this is a one-to-one or one-to-many relationship]")]
	public EAggRelKind RelKind { get; }

	[Doc(@"""
#Sum[Function that extracts the join key from a root entity.]
#Descr[Used to determine which related entities belong to a given root. Example: For WordProp items, this extracts the WordId from a Word root entity.]
""")]
	public Func<obj, obj?> FnFKeyToRootIdObj { get; }
}

[Doc("""
#Sum[Strongly-typed registration of a single *related entity*(not root entity) set in an aggregate.]
#Descr[Stores configuration for loading related entities of type TPo,
grouped by key TKey from a root entity.
Example: In a Word aggregate:

new AggIncludeReg<PoWordProp, IdWord>(Tbl_Prop, "WordId", EAggRelKind.OneToMany, x => x.WordId)]

#TParams(
	[The type of related entities (e.g., PoWordProp)],
	[The key type used to group related entities (e.g., IdWord)]
)
""")]
public partial class AggIncludeReg<TPo, TKey>
	: IAggIncludeReg
	where TPo : new() {
	public Type EntityType => typeof(TPo);

	public Func<obj> FnNewEntityObj => () => new TPo()!;

	[Doc(@"#Sum[The column name in this entity's table used for joining to the root]")]
	public str FKeyCodeCol { get; set; }

	[Doc(@"#Sum[The table definition containing entities of type TPo]")]
	public ITable Tbl { get; set; }

	[Doc(@"#Sum[The relationship kind (OneToOne or OneToMany)]")]
	public EAggRelKind RelKind { get; set; } = EAggRelKind.OneToMany;

	[Doc(@"""
#Sum[Strongly-typed function to extract the join key from a TPo instance.]
#Descr[Example: x => x.WordId]
""")]
	public Func<TPo, TKey> FnFKeyToRootId { get; set; }

	[Doc(@"#Sum[Object-typed wrapper of FnMemb for runtime use]")]
	public Func<obj, obj?> FnFKeyToRootIdObj => x => FnFKeyToRootId((TPo)x);

	[Doc("""
#Sum[Create a new strongly-typed include registration.]
#Descr[Example: var reg = new AggIncludeReg<PoWordProp, IdWord>(Tbl_Prop.Tbl, "WordId", EAggRelKind.OneToMany, x => x.WordId);]
""")]
	public AggIncludeReg(
		ITable<TPo> Tbl
		, str FKeyCodeCol
		, EAggRelKind RelKind
		, Func<TPo, TKey> FnFKeyToRootId
	) {
		this.Tbl = Tbl;
		this.FKeyCodeCol = FKeyCodeCol;
		this.RelKind = RelKind;
		this.FnFKeyToRootId = FnFKeyToRootId;
	}
}

[Doc(@"""
#Sum[Metadata describing how to load and assemble a complete aggregate from its root and related entities.]
#Descr[An aggregate is a graph of related entities centered around a root entity. Example: A JnWord aggregate consists of: - Root: PoWord (the main entity) - Includes: PoWordProp and PoWordLearn (loaded separately and attached to the root) - Assembly: Creates a JnWord(root, props, learns) from the root and loaded related entities. The registration system works in 2 phases: 1. LOAD: Load root entities by their IDs, then load all related entities 2. ASSEMBLE: For each root, invoke FnAssemble with the root and a context containing pre-loaded related entities]
""")]
public partial interface IAggReg {
	[Doc(@$"#Sum[The aggregate type being registered e.g., typeof(JnWord)].
	suggest it implement {nameof(IAgg)}
	")]
	public Type AggType { get; }

	[Doc(@"#Sum[The type of the root entity e.g., typeof(PoWord)]")]
	public Type RootEntityType { get; }

	[Doc(@"#Sum[The type of the root entity's ID e.g., typeof(IdWord)]")]
	public Type RootIdType { get; }

	[Doc(@"#Sum[The table definition for root entities, e.g ITable<PoWord>]")]
	public ITable RootTbl { get; }

	[Doc(@"""
#Sum[Function that extracts the ID from a root entity obj.]
#Examples([
fn(PoUser) returns PoUser.Id
])
""")]
	public Func<obj, obj?> FnGetIdFromRootObj { get; }

	[Doc(@"""
#Sum[Function that assembles a complete aggregate from a root and pre-loaded related entities.]
#Descr[
	The context parameter contains all related entities grouped by type and key.
	Example:
```cs
(root, ctx) => new JnWord(
	root, ctx.GetMany<PoWordProp, IdWord>(root.Id), ...
)
```
]
""")]
	public Func<obj, IAggQryCtx, obj> FnAssembleAggObj { get; }

	[Doc(@"""
#Sum[List of all included related entity sets.]
#Descr[Each entry describes a one-to-many or one-to-one relationship.]
""")]
	public IList<IAggIncludeReg> Includes { get; }
}

[Doc("""
#Sum[Strongly-typed registration of a complete aggregate.]
#Descr[Defines how to load and assemble a TAgg aggregate from its TRoot root entity
and related entities identified by TRootId.
Example:
var agg = AggReg<JnWord, PoWord, IdWord>.Mk(
	Tbl_Word.Tbl
	,x => x.Id
	,(root, ctx) => new JnWord(root, ctx.GetMany<PoWordProp, IdWord>(root.Id), ctx.GetMany<PoWordLearn, IdWord>(root.Id))).AddOneToMany(Tbl_Prop.Tbl, "WordId", x => x.WordId).AddOneToMany(Tbl_Learn.Tbl, "WordId", x => x.WordId); When BatSlctAggById is called with Word IDs: 1. Loads PoWord rows for each ID 2. Loads all PoWordProp rows where WordId matches 3. Loads all PoWordLearn rows where WordId matches 4. For each root PoWord, calls FnAssemble with the root and a context containing the matched related entities 5. Returns the assembled JnWord aggregates]
#TParams([The aggregate root type (e.g., JnWord)],[The persistent root entity type (e.g., PoWord)],[The ID type of the root entity (e.g., IdWord)])
""")]
public partial class AggReg<TAgg, TRoot, TRootId>
	: IAggReg
	where TRoot : class, new() {
	public Type AggType => typeof(TAgg);
	public Type RootEntityType => typeof(TRoot);
	public Type RootIdType => typeof(TRootId);

	[Doc(@"#Sum[The table definition for root entities]")]
	public ITable RootTbl { get; set; }

	[Doc(@"#Sum[Strongly-typed function to extract the ID from a root entity]")]
	public Func<TRoot, TRootId> FnGetIdFromRoot { get; set; }

	[Doc(@"#Sum[Object-typed wrapper of FnRootId]")]
	public Func<obj, obj?> FnGetIdFromRootObj => x => FnGetIdFromRoot((TRoot)x);

	[Doc(@"""
#Sum[Strongly-typed function that assembles the complete aggregate.]
#Descr[Parameters: - TRoot: The root entity instance - IAggBuildCtx: Context containing pre-loaded related entities Returns: The assembled TAgg instance Example: (root, ctx) => new JnWord(root, ctx.GetMany<PoWordProp, IdWord>(root.Id), ...)]
""")]
	public Func<TRoot, IAggQryCtx, TAgg> FnAssembleAgg { get; set; }

	[Doc(@"#Sum[Object-typed wrapper of FnAssemble]")]
	public Func<obj, IAggQryCtx, obj> FnAssembleAggObj => (root, ctx) => FnAssembleAgg((TRoot)root, ctx)!;

	[Doc(@"#Sum[List of included related entity sets (stored as mutable list internally)]")]
	public IList<IAggIncludeReg> _Includes { get; set; } = [];

	[Doc(@"#Sum[Read-only list of included related entity sets]")]
	public IList<IAggIncludeReg> Includes => _Includes;//原潙 _Includes.AsReadOnly();

	[Doc(@"""
#Sum[Create a new aggregate registration.]
#Examples([
```cs
new AggReg<JnWord, PoWord, IdWord>(
	Tbl_Word.Tbl, x => x.Id
	,(root, ctx) => new JnWord(
		root, ctx.GetMany<PoWordProp, IdWord>(root.Id)
		,...
	)
)
```
])
#Params(
	[The table containing root entities],
	[Function to extract the ID from a root entity],
	[Function to assemble the aggregate from root and context],
)
""")]
	public AggReg(
		ITable<TRoot> RootTbl
		, Func<TRoot, TRootId> FnGetIdFromRoot
		, Func<TRoot, IAggQryCtx, TAgg> FnAssembleAgg
	) {
		this.RootTbl = RootTbl;
		this.FnGetIdFromRoot = FnGetIdFromRoot;
		this.FnAssembleAgg = FnAssembleAgg;
	}

	[Doc(@"""
#Sum[Static factory method to create an aggregate registration.]
#Descr[Equivalent to calling the constructor directly.]
""")]
	public static AggReg<TAgg, TRoot, TRootId> Mk(
		ITable<TRoot> RootTbl
		, Func<TRoot, TRootId> FnRootId
		, Func<TRoot, IAggQryCtx, TAgg> FnAssemble
	) {
		return new AggReg<TAgg, TRoot, TRootId>(RootTbl, FnRootId, FnAssemble);
	}

	[Doc("""
#Sum[Add a related entity set to this aggregate with unspecified relationship kind.]
#Descr[Use AddOneToOne or AddOneToMany for convenience methods instead. Example: .AddInclude(Tbl_Prop.Tbl, "WordId", EAggRelKind.OneToMany, x => x.WordId)]
#Params([The table containing related entities],[The column name to join on (e.g., "WordId")],[The relationship kind (OneToOne or OneToMany)],[Function to extract the join key from a related entity])
#Rtn[This registration for method chaining]
""")]
	public AggReg<TAgg, TRoot, TRootId> AddInclude<TSubPo, TSubKey>(
		ITable<TSubPo> SubTbl
		, str SubCodeCol
		, EAggRelKind RelKind
		, Func<TSubPo, TSubKey> FnGetSubFKeyToRoot
	)
		where TSubPo : new() {
		_Includes.Add(new AggIncludeReg<TSubPo, TSubKey>(SubTbl, SubCodeCol, RelKind, FnGetSubFKeyToRoot));
		return this;
	}

	[Doc("""
return AddInclude(Tbl, CodeCol, EAggRelKind.OneToOne, FnMemb);
""")]
	public AggReg<TAgg, TRoot, TRootId> AddOneToOne<TPo, TKey>(
		ITable<TPo> Tbl
		, str CodeCol
		, Func<TPo, TKey> FnMemb
	)
		where TPo : new()
	{
		return AddInclude(Tbl, CodeCol, EAggRelKind.OneToOne, FnMemb);
	}

	[Doc("""
return AddInclude(Tbl, CodeCol, EAggRelKind.OneToMany, FnMemb);
""")]
	public AggReg<TAgg, TRoot, TRootId> AddOneToMany<TPo, TKey>(
		ITable<TPo> Tbl
		, str CodeCol
		, Func<TPo, TKey> FnMemb
	)
		where TPo : new()
	{
		return AddInclude(Tbl, CodeCol, EAggRelKind.OneToMany, FnMemb);
	}
}
