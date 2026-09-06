using System.Collections;

namespace Tsinswreng.CsSql;

/// <summary>
/// Shared async source state for projected Many(...) binders on the same source.
/// </summary>
internal sealed class SharedManyAsyMapBatchSource<TItem>{
	private readonly IAsyncEnumerable<TItem> _items;
	private IAsyncEnumerator<TItem>? _itor;
	private IList? _currentBatch;
	private i32 _binderCount = 0;
	private i32 _remainingFollowers = 0;

	public SharedManyAsyMapBatchSource(IAsyncEnumerable<TItem> Items){
		_items = Items;
	}

	public i32 RegisterBinder(){
		var idx = _binderCount;
		_binderCount++;
		return idx;
	}

	public async ValueTask<(bool HasAny, IList Batch)> TakeBatchAsync(
		i32 BinderIndex,
		u64 BatchSize,
		CT Ct
	){
		if(BinderIndex == 0){
			var batch = new List<TItem>();
			_itor ??= _items.GetAsyncEnumerator(Ct);
			for(u64 i = 0; i < BatchSize; i++){
				if(!await _itor.MoveNextAsync()){
					break;
				}
				batch.Add(_itor.Current);
			}
			if(batch.Count == 0){
				return (false, batch);
			}

			// Single binder case: no need to keep current batch.
			if(_binderCount <= 1){
				return (true, batch);
			}

			_currentBatch = batch;
			_remainingFollowers = _binderCount - 1;
			return (true, batch);
		}

		if(_currentBatch is null){
			throw new InvalidOperationException("Projected Many(...) secondary binder consumed before primary binder.");
		}
		if((u64)_currentBatch.Count != BatchSize){
			throw new InvalidOperationException("Projected Many(...) batch size mismatch.");
		}
		var ans = _currentBatch;
		_remainingFollowers--;
		if(_remainingFollowers == 0){
			_currentBatch = null;
		}
		return (ans.Count > 0, ans);
	}
}

/// <summary>
/// Async binder that maps each shared source item to parameter value.
/// </summary>
internal sealed class ParamAutoBinderManyAsyMap<TItem, TVal>: IParamAutoBinderMultiAsy{
	public IParam Param { get; set; }
	public ITable? Tbl { get; set; }
	/// 值所屬的 Code 列名:非空時按列 Upper→Raw(精確列轉換);空時退回按類型默認映射(單參重載)。
	public str? CodeCol { get; set; }
	public Func<TItem, TVal> FnMap { get; set; }
	private SharedManyAsyMapBatchSource<TItem> SharedSource { get; set; }
	private i32 BinderIndex { get; set; }

	public ParamAutoBinderManyAsyMap(
		IParam Param,
		SharedManyAsyMapBatchSource<TItem> SharedSource,
		i32 BinderIndex,
		Func<TItem, TVal> FnMap
	){
		this.Param = Param;
		this.SharedSource = SharedSource;
		this.BinderIndex = BinderIndex;
		this.FnMap = FnMap;
	}

	public void Bind(IArgDict Args){
		throw new NotSupportedException("Use TryTakeBatchArgsAsync for async streaming");
	}

	public ValueTask<(bool HasAny, IList Batch)> TryTakeBatchArgsAsync(u64 BatchSize, CT Ct){
		return SharedSource.TakeBatchAsync(BinderIndex, BatchSize, Ct);
	}

	public void BindBatch(IArgDict Args, IList Batch){
		var mapped = new List<TVal>(Batch.Count);
		foreach(var item in Batch){
			if(item is not TItem typed){
				throw new InvalidCastException($"Expected item type {typeof(TItem).Name}, got {item?.GetType().Name ?? "null"}.");
			}
			mapped.Add(FnMap(typed));
		}

		foreach(var (i, value) in mapped.Index()){
			var p = Param.ToOfst((u64)i);
			Args.AddRaw(p, ToRaw(value));
		}
	}

	/// 值 → 原始層:帶 Tbl 時執行 Upper→Raw;有 CodeCol 按列精確轉換,否則按類型默認映射。
	private obj? ToRaw(TVal Value){
		if(Tbl is null){
			return Value;
		}
		if(CodeCol != null){
			return Tbl.UpperToRaw(Value, CodeCol);
		}
		return Tbl.UpperToRaw(Value);
	}
}

