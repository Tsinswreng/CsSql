using Tsinswreng.CsTreeTest;
using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Test.Domains;

namespace Tsinswreng.CsSql.Test.CsSql.Repo;

public partial class TestRepo {
	readonly List<TestKv> _batUpdEnts = new();
	readonly List<IdTestKv> _batUpdIds = new();

	void RegisterBatUpd(ITestNode Node) {
		var register = Node.MkTestFnRegister(
			typeof(TestRepo)
			,[typeof(IRepo<TestKv, IdTestKv>)]
			,[]
		);
		var R = register.Register;
		var T = Assert.IsTrue;

		register.TesteeFnNames = [nameof(IRepo<TestKv, IdTestKv>.OrdAdd)];
		R("BatUpd_Insert_Multi", async(o)=>{
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var ents = new List<TestKv>();
				for (var i = 0; i < 3; i++) {
					var e = new TestKv{
						Id = new IdTestKv(),
						Owner = IdTestUser.Zero,
						KStr = "bat_upd_k_" + System.Guid.NewGuid().ToString("N"),
						VStr = "bat_upd_v_" + System.Guid.NewGuid().ToString("N"),
					};
					ents.Add(e);
				}

				var resp = await Repo.OrdAdd(Ctx, AsyE(ents.ToArray()), CT.None);
				T(resp is not null, "BatInsert returned null response");

				_batUpdEnts.Clear();
				_batUpdIds.Clear();
				_batUpdEnts.AddRange(ents);
				_batUpdIds.AddRange(ents.Select(x=>x.Id));
				return NIL;
			});
		});

		register.TesteeFnNames = [
			nameof(IRepo<TestKv, IdTestKv>.OrdUpd)
			,nameof(IRepo<TestKv, IdTestKv>.OrdGetByIdWithDel)
		];
		R("BatUpd_ById", async(o)=>{
			if (_batUpdIds.Count == 0) {
				T(false, "BatUpd_Insert_Multi not executed or no ids recorded");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var upds = new List<TestKv>();
				for (var i = 0; i < _batUpdEnts.Count; i++) {
					var src = _batUpdEnts[i];
					upds.Add(new TestKv{
						Id = src.Id,
						Owner = src.Owner,
						KStr = "bat_upd_k2_" + System.Guid.NewGuid().ToString("N"),
						VStr = "bat_upd_v2_" + System.Guid.NewGuid().ToString("N"),
					});
				}
				var resp = await Repo.OrdUpd(Ctx, AsyE(upds.ToArray()), CT.None);
				T(resp is not null, "BatUpdById returned null response");

				var verify = Repo.OrdGetByIdWithDel(Ctx, AsyE(_batUpdIds.ToArray()), CT.None);
				var list = new List<TestKv?>();
				await foreach (var item in verify) list.Add(item);
				T(list.Count == _batUpdIds.Count, $"Expected {_batUpdIds.Count} entries, got {list.Count}");
				for (var i = 0; i < list.Count; i++) {
					var got = list[i];
					T(got is not null, $"Expected non-null entity at index {i}");
					if (got is null) {
						continue;
					}
					var exp = upds[i];
					T(got.Id.Equals(exp.Id), $"Id mismatch at index {i}");
					T(got.KStr == exp.KStr && got.VStr == exp.VStr, $"Value mismatch at index {i}");
				}
				_batUpdEnts.Clear();
				_batUpdEnts.AddRange(upds);
				return NIL;
			});
		});

		register.TesteeFnNames = [
			nameof(IRepo<TestKv, IdTestKv>.OrdUpdByCodeDict)
			,nameof(IRepo<TestKv, IdTestKv>.OrdGetByIdWithDel)
		];
		R("BatUpd_ByCodeDict", async(o)=>{
			if (_batUpdIds.Count == 0) {
				T(false, "BatUpd_Insert_Multi not executed or no ids recorded");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var dicts = new List<IDictionary<str, obj?>>();
				var expK = new List<str?>();
				var expV = new List<str?>();
				for (var i = 0; i < _batUpdIds.Count; i++) {
					var k = "bat_upd_k3_" + System.Guid.NewGuid().ToString("N");
					var v = "bat_upd_v3_" + System.Guid.NewGuid().ToString("N");
					expK.Add(k);
					expV.Add(v);
					dicts.Add(new Dictionary<str, obj?>{
						[nameof(TestKv.KStr)] = k,
						[nameof(TestKv.VStr)] = v,
					});
				}
				var resp = await Repo.OrdUpdByCodeDict(Ctx, AsyE(_batUpdIds.ToArray()), AsyE(dicts.ToArray()), CT.None);
				T(resp is not null, "BatUpdByCodeDict returned null response");
				var verify = Repo.OrdGetByIdWithDel(Ctx, AsyE(_batUpdIds.ToArray()), CT.None);
				var list = new List<TestKv?>();
				await foreach (var item in verify) list.Add(item);
				for (var i = 0; i < list.Count; i++) {
					var got = list[i];
					T(got is not null, $"Expected non-null entity at index {i}");
					if (got is null) {
						continue;
					}
					T(got.KStr == expK[i] && got.VStr == expV[i], $"Value mismatch at index {i}");
				}
				return NIL;
			});
		});

		register.TesteeFnNames = [
			nameof(IRepo<TestKv, IdTestKv>.OrdUpdByDbDict)
			,nameof(IRepo<TestKv, IdTestKv>.OrdGetByIdWithDel)
		];
		R("BatUpd_ByDbDict", async(o)=>{
			if (_batUpdIds.Count == 0) {
				T(false, "BatUpd_Insert_Multi not executed or no ids recorded");
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var dicts = new List<IDictionary<str, obj?>>();
				var expV = new List<str?>();
				for (var i = 0; i < _batUpdIds.Count; i++) {
					var v = "bat_upd_v4_" + System.Guid.NewGuid().ToString("N");
					expV.Add(v);
					dicts.Add(new Dictionary<str, obj?>{
						[nameof(TestKv.VStr)] = v,
					});
				}
				var resp = await Repo.OrdUpdByDbDict(Ctx, AsyE(_batUpdIds.ToArray()), AsyE(dicts.ToArray()), CT.None);
				T(resp is not null, "BatUpdByDbDict returned null response");
				var verify = Repo.OrdGetByIdWithDel(Ctx, AsyE(_batUpdIds.ToArray()), CT.None);
				var list = new List<TestKv?>();
				await foreach (var item in verify) list.Add(item);
				for (var i = 0; i < list.Count; i++) {
					var got = list[i];
					T(got is not null, $"Expected non-null entity at index {i}");
					if (got is null) {
						continue;
					}
					T(got.VStr == expV[i], $"Value mismatch at index {i}");
				}
				return NIL;
			});
		});

		register.TesteeFnNames = [
			nameof(IRepo<TestKv, IdTestKv>.OrdHardDelById)
			,nameof(IRepo<TestKv, IdTestKv>.OrdGetByIdWithDel)
		];
		R("BatUpd_Cleanup_HardDelete", async(o)=>{
			if (_batUpdIds.Count == 0) {
				return NIL;
			}
			return await RunInTxnIfNoCtx(async(Ctx)=>{
				var resp = await Repo.OrdHardDelById(Ctx, AsyE(_batUpdIds.ToArray()), CT.None);
				T(resp is not null, "BatHardDelById returned null response");
				var verify = Repo.OrdGetByIdWithDel(Ctx, AsyE(_batUpdIds.ToArray()), CT.None);
				var list = new List<TestKv?>();
				await foreach (var item in verify) list.Add(item);
				T(!list.Any(x=>x != null), "Expected all nulls after hard delete");
				return NIL;
			});
		});
	}
}
