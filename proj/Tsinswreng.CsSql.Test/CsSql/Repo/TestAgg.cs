using Tsinswreng.CsTreeTest;
using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Test.Domains;

namespace Tsinswreng.CsSql.Test.CsSql.Repo;

public partial class TestRepo {
	readonly List<IdTestWord> _aggWordIds = new();
	readonly List<IdTestWordProp> _aggPropIds = new();
	readonly List<IdTestWordLearn> _aggLearnIds = new();
	readonly List<IdTestWordProp> _aggPrevPropIds = new();
	readonly List<IdTestWordLearn> _aggPrevLearnIds = new();

	void RegisterAgg(ITestNode Node) {
		var register = Node.MkTestFnRegister(
			typeof(TestRepo)
			,[
				typeof(IRepo<TestWord, IdTestWord>)
				,typeof(IRepo<TestWordProp, IdTestWordProp>)
				,typeof(IRepo<TestWordLearn, IdTestWordLearn>)
			]
			,[]
		);
		var R = register.Register;
		var T = Assert.IsTrue;

		register.TesteeFnNames = [nameof(IRepo<TestWord, IdTestWord>.OrdAddAgg)];
		R("Agg_Insert_By_BatAddAgg", async(o)=>{
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var batch = new List<TestJnWord>();
				for (var i = 0; i < 2; i++) {
					var word = new TestWord{
						Id = new IdTestWord(),
						Owner = IdTestUser.Zero,
						Head = "agg_word_" + System.Guid.NewGuid().ToString("N"),
						Lang = "en",
					};
					var prop1 = new TestWordProp{
						Id = new IdTestWordProp(),
						WordId = word.Id,
						KStr = "tag",
						VStr = "v_" + System.Guid.NewGuid().ToString("N"),
					};
					var learn1 = new TestWordLearn{
						Id = new IdTestWordLearn(),
						WordId = word.Id,
						LearnResult = "add",
					};
					batch.Add(new TestJnWord{
						Word = word,
						Props = [prop1],
						Learns = [learn1],
					});
				}

				var resp = await RepoWord.OrdAddAgg<TestJnWord>(Ctx, AsyE(batch.ToArray()), CT.None);
				T(resp is not null, "BatAddAgg returned null response");

				_aggWordIds.Clear();
				_aggPropIds.Clear();
				_aggLearnIds.Clear();
				_aggWordIds.AddRange(batch.Select(x=>x.Word.Id));
				_aggPropIds.AddRange(batch.SelectMany(x=>x.Props.Select(y=>y.Id)));
				_aggLearnIds.AddRange(batch.SelectMany(x=>x.Learns.Select(y=>y.Id)));
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestWord, IdTestWord>.OrdGetAggByIdWithDel)];
		R("Agg_BatGet_By_Id", async(o)=>{
			if (_aggWordIds.Count == 0) {
				T(false, "Agg_Insert_By_BatAddAgg not executed");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var gotAsy = RepoWord.OrdGetAggByIdWithDel<TestJnWord>(Ctx, AsyE(_aggWordIds.ToArray()), CT.None);
				var got = new List<TestJnWord?>();
				await foreach (var item in gotAsy) {
					got.Add(item);
				}
				T(got.Count == _aggWordIds.Count, $"Expected {_aggWordIds.Count} rows, got {got.Count}");
				for (var i = 0; i < got.Count; i++) {
					var one = got[i];
					T(one is not null, $"Expected non-null aggregate at index {i}");
					T(one is not null && one.Word.Id.Equals(_aggWordIds[i]), $"Word id mismatch at index {i}");
				}
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestWord, IdTestWord>.GetAllAgg)];
		R("Agg_GetAllAgg_Should_Contain_Inserted", async(o)=>{
			if (_aggWordIds.Count == 0) {
				T(false, "Agg_Insert_By_BatAddAgg not executed");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var gotAsy = RepoWord.GetAllAgg<TestJnWord>(Ctx, CT.None);
				var found = new HashSet<IdTestWord>();
				await foreach (var agg in gotAsy) {
					if (_aggWordIds.Contains(agg.Word.Id)) {
						found.Add(agg.Word.Id);
					}
				}
				foreach (var id in _aggWordIds) {
					T(found.Contains(id), $"GetAllAgg missing inserted word id: {id}");
				}
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestWord, IdTestWord>.OrdHardUpdAgg)];
		R("Agg_HardUpd_Should_Replace_Includes", async(o)=>{
			if (_aggWordIds.Count == 0) {
				T(false, "Agg_Insert_By_BatAddAgg not executed");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				_aggPrevPropIds.Clear();
				_aggPrevLearnIds.Clear();
				_aggPrevPropIds.AddRange(_aggPropIds);
				_aggPrevLearnIds.AddRange(_aggLearnIds);

				var upds = new List<TestJnWord>();
				_aggPropIds.Clear();
				_aggLearnIds.Clear();
				foreach (var wordId in _aggWordIds) {
					var w = new TestWord{
						Id = wordId,
						Owner = IdTestUser.Zero,
						Head = "agg_hard_upd_" + System.Guid.NewGuid().ToString("N"),
						Lang = "en",
					};
					var p = new TestWordProp{
						Id = new IdTestWordProp(),
						WordId = wordId,
						KStr = "hard",
						VStr = "hard_" + System.Guid.NewGuid().ToString("N"),
					};
					var l = new TestWordLearn{
						Id = new IdTestWordLearn(),
						WordId = wordId,
						LearnResult = "rmb",
					};
					upds.Add(new TestJnWord{
						Word = w,
						Props = [p],
						Learns = [l],
					});
					_aggPropIds.Add(p.Id);
					_aggLearnIds.Add(l.Id);
				}

				await RepoWord.OrdHardUpdAgg<TestJnWord>(Ctx, AsyE(upds.ToArray()), CT.None);

				var got = RepoWord.OrdGetAggByIdWithDel<TestJnWord>(Ctx, AsyE(_aggWordIds.ToArray()), CT.None);
				await foreach (var one in got) {
					T(one is not null && one.Props.Count == 1 && one.Learns.Count == 1, "HardUpd result mismatch");
				}

				var oldProps = RepoProp.OrdGetByIdWithDel(Ctx, AsyE(_aggPrevPropIds.ToArray()), CT.None);
				await foreach (var old in oldProps) {
					T(old is null, "HardUpd should hard-delete removed props");
				}
				var oldLearns = RepoLearn.OrdGetByIdWithDel(Ctx, AsyE(_aggPrevLearnIds.ToArray()), CT.None);
				await foreach (var old in oldLearns) {
					T(old is null, "HardUpd should hard-delete removed learns");
				}
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestWord, IdTestWord>.OrdSoftUpdAgg)];
		R("Agg_SoftUpd_Should_SoftDelete_Missing", async(o)=>{
			if (_aggWordIds.Count == 0) {
				T(false, "Agg_HardUpd_Should_Replace_Includes not executed");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				_aggPrevPropIds.Clear();
				_aggPrevLearnIds.Clear();
				_aggPrevPropIds.AddRange(_aggPropIds);
				_aggPrevLearnIds.AddRange(_aggLearnIds);

				var upds = new List<TestJnWord>();
				_aggPropIds.Clear();
				_aggLearnIds.Clear();
				foreach (var wordId in _aggWordIds) {
					var w = new TestWord{
						Id = wordId,
						Owner = IdTestUser.Zero,
						Head = "agg_soft_upd_" + System.Guid.NewGuid().ToString("N"),
						Lang = "en",
					};
					var p = new TestWordProp{
						Id = new IdTestWordProp(),
						WordId = wordId,
						KStr = "soft",
						VStr = "soft_" + System.Guid.NewGuid().ToString("N"),
					};
					var l = new TestWordLearn{
						Id = new IdTestWordLearn(),
						WordId = wordId,
						LearnResult = "add",
					};
					upds.Add(new TestJnWord{
						Word = w,
						Props = [p],
						Learns = [l],
					});
					_aggPropIds.Add(p.Id);
					_aggLearnIds.Add(l.Id);
				}

				await RepoWord.OrdSoftUpdAgg<TestJnWord>(Ctx, AsyE(upds.ToArray()), CT.None);

				var oldProps = RepoProp.OrdGetByIdWithDel(Ctx, AsyE(_aggPrevPropIds.ToArray()), CT.None);
				await foreach (var old in oldProps) {
					T(old is not null && old.DelAt != 0, "SoftUpd should soft-delete removed props");
				}
				var oldLearns = RepoLearn.OrdGetByIdWithDel(Ctx, AsyE(_aggPrevLearnIds.ToArray()), CT.None);
				await foreach (var old in oldLearns) {
					T(old is not null && old.DelAt != 0, "SoftUpd should soft-delete removed learns");
				}
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestWord, IdTestWord>.SoftDelAggInId)];
		R("Agg_SoftDelete_Root_And_Includes", async(o)=>{
			if (_aggWordIds.Count == 0) {
				T(false, "Agg_Insert_By_BatAddAgg not executed");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var resp = await RepoWord.SoftDelAggInId<TestJnWord>(Ctx, AsyE(_aggWordIds.ToArray()), CT.None);
				T(resp is not null, "SoftDelAggInId returned null response");

				var wordsAsy = RepoWord.OrdGetByIdWithDel(Ctx, AsyE(_aggWordIds.ToArray()), CT.None);
				await foreach (var w in wordsAsy) {
					T(w is not null && w.DelAt != 0, "Expected all word rows soft deleted");
				}
				var propsAsy = RepoProp.OrdGetByIdWithDel(Ctx, AsyE(_aggPropIds.ToArray()), CT.None);
				await foreach (var p in propsAsy) {
					T(p is not null && p.DelAt != 0, "Expected all prop rows soft deleted");
				}
				var learnsAsy = RepoLearn.OrdGetByIdWithDel(Ctx, AsyE(_aggLearnIds.ToArray()), CT.None);
				await foreach (var l in learnsAsy) {
					T(l is not null && l.DelAt != 0, "Expected all learn rows soft deleted");
				}
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestWord, IdTestWord>.OrdGetAggById)];
		R("Agg_BatGet_By_Id_NonWithDel_Should_Exclude_SoftDeleted", async(o)=>{
			if (_aggWordIds.Count == 0) {
				T(false, "Agg_SoftDelete_Root_And_Includes not executed");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var gotAsy = RepoWord.OrdGetAggById<TestJnWord>(Ctx, AsyE(_aggWordIds.ToArray()), CT.None);
				var got = new List<TestJnWord?>();
				await foreach (var one in gotAsy) {
					got.Add(one);
				}
				T(got.Count == _aggWordIds.Count, $"Expected {_aggWordIds.Count} entries, got {got.Count}");
				T(!got.Any(x=>x is not null), "BatGetAggById should not return soft-deleted roots");
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestWord, IdTestWord>.OrdGetAggByIdWithDel)];
		R("Agg_BatGet_By_Id_WithDel_Should_Include_SoftDeleted", async(o)=>{
			if (_aggWordIds.Count == 0) {
				T(false, "Agg_SoftDelete_Root_And_Includes not executed");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var gotAsy = RepoWord.OrdGetAggByIdWithDel<TestJnWord>(Ctx, AsyE(_aggWordIds.ToArray()), CT.None);
				var got = new List<TestJnWord?>();
				await foreach (var one in gotAsy) {
					got.Add(one);
				}
				T(got.Count == _aggWordIds.Count, $"Expected {_aggWordIds.Count} entries, got {got.Count}");
				T(!got.Any(x=>x is null), "BatGetAggByIdWithDel should return soft-deleted roots");
				T(!got.Any(x=>x is not null && x.Word.DelAt == 0), "WithDel aggregate root should carry deleted flag");
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestWord, IdTestWord>.OrdGetAggById)];
		R("Agg_BatGet_By_Id_NonWithDel_Should_Exclude_SoftDeleted_Includes_When_Root_Is_Alive", async(o)=>{
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var owner = new IdTestUser();
				var word = new TestWord{
					Id = new IdTestWord(),
					Owner = owner,
					Head = "agg_nonwithdel_include_" + System.Guid.NewGuid().ToString("N"),
					Lang = "en",
				};
				var keepProp = new TestWordProp{
					Id = new IdTestWordProp(),
					WordId = word.Id,
					KStr = "keep_prop",
					VStr = "keep_" + System.Guid.NewGuid().ToString("N"),
				};
				var delProp = new TestWordProp{
					Id = new IdTestWordProp(),
					WordId = word.Id,
					KStr = "del_prop",
					VStr = "del_" + System.Guid.NewGuid().ToString("N"),
				};
				var keepLearn = new TestWordLearn{
					Id = new IdTestWordLearn(),
					WordId = word.Id,
					LearnResult = "add",
				};
				var delLearn = new TestWordLearn{
					Id = new IdTestWordLearn(),
					WordId = word.Id,
					LearnResult = "rmb",
				};
				try {
					await RepoWord.OrdAdd(Ctx, AsyE(word), CT.None);
					await RepoProp.OrdAdd(Ctx, AsyE(keepProp, delProp), CT.None);
					await RepoLearn.OrdAdd(Ctx, AsyE(keepLearn, delLearn), CT.None);
					await RepoProp.SoftDelInId(Ctx, AsyE(delProp.Id), CT.None);
					await RepoLearn.SoftDelInId(Ctx, AsyE(delLearn.Id), CT.None);

					var got = await RepoWord.OrdGetAggById<TestJnWord>(Ctx, AsyE(word.Id), CT.None).FirstOrDefaultAsync(CT.None);
					T(got is not null, "OrdGetAggById should return alive root");
					if (got is null) {
						return NIL;
					}
					T(!got.Props.Any(x=>x.Id == delProp.Id), "OrdGetAggById should exclude soft-deleted props from aggregate includes");
					T(!got.Learns.Any(x=>x.Id == delLearn.Id), "OrdGetAggById should exclude soft-deleted learns from aggregate includes");
					T(got.Props.Any(x=>x.Id == keepProp.Id), "OrdGetAggById should keep non-deleted props");
					T(got.Learns.Any(x=>x.Id == keepLearn.Id), "OrdGetAggById should keep non-deleted learns");
					return NIL;
				} finally {
					await RepoWord.HardDelAggInId<TestJnWord>(Ctx, AsyE(word.Id), CT.None);
				}
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestWord, IdTestWord>.OrdGetAggByIdWithDel)];
		R("Agg_BatGet_By_Id_WithDel_Should_Include_SoftDeleted_Includes_When_Root_Is_Alive", async(o)=>{
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var owner = new IdTestUser();
				var word = new TestWord{
					Id = new IdTestWord(),
					Owner = owner,
					Head = "agg_withdel_include_" + System.Guid.NewGuid().ToString("N"),
					Lang = "en",
				};
				var keepProp = new TestWordProp{
					Id = new IdTestWordProp(),
					WordId = word.Id,
					KStr = "keep_prop",
					VStr = "keep_" + System.Guid.NewGuid().ToString("N"),
				};
				var delProp = new TestWordProp{
					Id = new IdTestWordProp(),
					WordId = word.Id,
					KStr = "del_prop",
					VStr = "del_" + System.Guid.NewGuid().ToString("N"),
				};
				var keepLearn = new TestWordLearn{
					Id = new IdTestWordLearn(),
					WordId = word.Id,
					LearnResult = "add",
				};
				var delLearn = new TestWordLearn{
					Id = new IdTestWordLearn(),
					WordId = word.Id,
					LearnResult = "rmb",
				};
				try {
					await RepoWord.OrdAdd(Ctx, AsyE(word), CT.None);
					await RepoProp.OrdAdd(Ctx, AsyE(keepProp, delProp), CT.None);
					await RepoLearn.OrdAdd(Ctx, AsyE(keepLearn, delLearn), CT.None);
					await RepoProp.SoftDelInId(Ctx, AsyE(delProp.Id), CT.None);
					await RepoLearn.SoftDelInId(Ctx, AsyE(delLearn.Id), CT.None);

					var got = await RepoWord.OrdGetAggByIdWithDel<TestJnWord>(Ctx, AsyE(word.Id), CT.None).FirstOrDefaultAsync(CT.None);
					T(got is not null, "OrdGetAggByIdWithDel should return alive root");
					if (got is null) {
						return NIL;
					}
					T(got.Props.Any(x=>x.Id == delProp.Id && x.DelAt != 0), "OrdGetAggByIdWithDel should include soft-deleted props in aggregate includes");
					T(got.Learns.Any(x=>x.Id == delLearn.Id && x.DelAt != 0), "OrdGetAggByIdWithDel should include soft-deleted learns in aggregate includes");
					return NIL;
				} finally {
					await RepoWord.HardDelAggInId<TestJnWord>(Ctx, AsyE(word.Id), CT.None);
				}
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestWord, IdTestWord>.GetAllAgg)];
		R("Agg_GetAllAgg_NonWithDel_Should_Exclude_SoftDeleted", async(o)=>{
			if (_aggWordIds.Count == 0) {
				T(false, "Agg_SoftDelete_Root_And_Includes not executed");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var gotAsy = RepoWord.GetAllAgg<TestJnWord>(Ctx, CT.None);
				var found = new HashSet<IdTestWord>();
				await foreach (var one in gotAsy) {
					if (_aggWordIds.Contains(one.Word.Id)) {
						found.Add(one.Word.Id);
					}
				}
				T(found.Count == 0, "GetAllAgg should exclude soft-deleted roots");
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestWord, IdTestWord>.GetAllAggWithDel)];
		R("Agg_GetAllAgg_WithDel_Should_Include_SoftDeleted", async(o)=>{
			if (_aggWordIds.Count == 0) {
				T(false, "Agg_SoftDelete_Root_And_Includes not executed");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var gotAsy = RepoWord.GetAllAggWithDel<TestJnWord>(Ctx, CT.None);
				var found = new HashSet<IdTestWord>();
				await foreach (var one in gotAsy) {
					if (_aggWordIds.Contains(one.Word.Id)) {
						found.Add(one.Word.Id);
						T(one.Word.DelAt != 0, "GetAllAggWithDel should return deleted roots with deleted flag");
					}
				}
				foreach (var id in _aggWordIds) {
					T(found.Contains(id), $"GetAllAggWithDel missing soft-deleted root: {id}");
				}
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestWord, IdTestWord>.HardDelAggInId)];
		R("Agg_HardDelete_Root_And_Includes", async(o)=>{
			if (_aggWordIds.Count == 0) {
				T(false, "Agg_Insert_By_BatAddAgg not executed");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var resp = await RepoWord.HardDelAggInId<TestJnWord>(Ctx, AsyE(_aggWordIds.ToArray()), CT.None);
				T(resp is not null, "HardDelAggInId returned null response");

				var wordsAsy = RepoWord.OrdGetByIdWithDel(Ctx, AsyE(_aggWordIds.ToArray()), CT.None);
				await foreach (var w in wordsAsy) {
					T(w is null, "Expected word row hard deleted");
				}
				var propsAsy = RepoProp.OrdGetByIdWithDel(Ctx, AsyE(_aggPropIds.ToArray()), CT.None);
				await foreach (var p in propsAsy) {
					T(p is null, "Expected prop row hard deleted");
				}
				var learnsAsy = RepoLearn.OrdGetByIdWithDel(Ctx, AsyE(_aggLearnIds.ToArray()), CT.None);
				await foreach (var l in learnsAsy) {
					T(l is null, "Expected learn row hard deleted");
				}
				return NIL;
			});
		});

		register.TesteeFnNames = [nameof(IRepo<TestWord, IdTestWord>.HardDelAggInId)];
		R("Agg_Cleanup_HardDelete", async(o)=>{
			if (_aggWordIds.Count == 0) {
				return NIL;
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				await RepoWord.HardDelAggInId<TestJnWord>(Ctx, AsyE(_aggWordIds.ToArray()), CT.None);
				return NIL;
			});
		});
	}
}
