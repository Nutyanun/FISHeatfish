using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
// ใช้ alias กันชนกับ System.*
using GDict = Godot.Collections.Dictionary;
using GArray = Godot.Collections.Array;

public static class LeaderboardStore
{
	public static string PlayersPath => "user://players.json";

	// ===== Helpers: Date =====
	public static string MakeTodayKeyLocal() => DateTime.Now.ToString("yyyy-MM-dd");

	public static string FormatThaiDateForHeader(string yyyyMmDd)
	{
		if (DateTime.TryParse(yyyyMmDd, out var d))
			return d.ToString("dd/MM/yyyy", new System.Globalization.CultureInfo("th-TH"));
		return yyyyMmDd;
	}

	// ===== Load / Save =====
	public static GDict LoadDoc()
	{
		if (!FileAccess.FileExists(PlayersPath)) return new GDict();
		using var f = FileAccess.Open(PlayersPath, FileAccess.ModeFlags.Read);
		var v = Json.ParseString(f.GetAsText());
		return v.VariantType == Variant.Type.Dictionary ? (GDict)v : new GDict();
	}

	public static void SaveDoc(GDict doc)
	{
		using var f = FileAccess.Open(PlayersPath, FileAccess.ModeFlags.Write);
		f.StoreString(Json.Stringify(doc, "\t"));
	}

	// ===== Ensure nodes =====
	public static GDict EnsureRoot(GDict doc)
	{
		if (!doc.ContainsKey("players"))      doc["players"] = new GDict();
		if (!doc.ContainsKey("leaderboards")) doc["leaderboards"] = new GDict();
		return doc;
	}

	public static GDict EnsureLeaderboardDate(GDict doc, string dateKey)
	{
		var lb = (GDict)doc["leaderboards"];
		if (!lb.ContainsKey(dateKey)) lb[dateKey] = new GDict();
		return (GDict)lb[dateKey];
	}

	public static GArray EnsureLeaderboardLevel(GDict dateNode, string levelKey)
	{
		if (!dateNode.ContainsKey(levelKey)) dateNode[levelKey] = new GArray();
		return (GArray)dateNode[levelKey];
	}

	// ===== Upsert & sort =====
	public static void UpsertScore(string dateKey, int levelIndex, string playerName, int score, int limitTop = 50)
	{
		var doc = EnsureRoot(LoadDoc());

		// 1) leaderboards
		var dateNode = EnsureLeaderboardDate(doc, dateKey);
		var levelKey = levelIndex.ToString();
		var arr = EnsureLeaderboardLevel(dateNode, levelKey);

		int foundIdx = -1;
		for (int i = 0; i < arr.Count; i++)
		{
			var row = (GDict)arr[i];
			var name = row.ContainsKey("name") ? row["name"].AsString() : "";
			if (name == playerName) { foundIdx = i; break; }
		}

		var nowIso = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

		if (foundIdx >= 0)
		{
			var row = (GDict)arr[foundIdx];
			int old = row.ContainsKey("score") ? (int)(long)row["score"] : 0;
			if (score > old)
			{
				row["score"] = score;
				row["updated_at"] = nowIso;
				arr[foundIdx] = row;
			}
		}
		else
		{
			var row = new GDict {
				{ "name", playerName },
				{ "score", score },
				{ "updated_at", nowIso }
			};
			arr.Add(row);
		}

		// sort: score desc, tie -> updated_at asc
		var list = new List<GDict>();
		foreach (var v in arr) list.Add((GDict)v);

		list.Sort((a, b) =>
		{
			int sa = (int)(long)a["score"];
			int sb = (int)(long)b["score"];
			int cmp = sb.CompareTo(sa);
			if (cmp != 0) return cmp;

			var ua = a.ContainsKey("updated_at") ? a["updated_at"].AsString() : "";
			var ub = b.ContainsKey("updated_at") ? b["updated_at"].AsString() : "";
			return string.Compare(ua, ub, StringComparison.Ordinal);
		});

		if (limitTop > 0 && list.Count > limitTop)
			list = list.Take(limitTop).ToList();

		var newArr = new GArray();
		foreach (var r in list) newArr.Add(r);
		dateNode[levelKey] = newArr;

		// 2) players: update personal high + history
		var players = (GDict)doc["players"];
		if (!players.ContainsKey(playerName))
		{
			players[playerName] = new GDict {
				{ "registered_at", nowIso },
				{ "levels", new GDict() }
			};
		}
		var pObj = (GDict)players[playerName];
		if (!pObj.ContainsKey("levels")) pObj["levels"] = new GDict();
		var levels = (GDict)pObj["levels"];
		if (!levels.ContainsKey(levelKey))
		{
			levels[levelKey] = new GDict {
				{ "high", 0 },
				{ "scores", new GArray() }
			};
		}
		var lvObj = (GDict)levels[levelKey];
		int oldHigh = lvObj.ContainsKey("high") ? (int)(long)lvObj["high"] : 0;
		if (score > oldHigh) lvObj["high"] = score;

		var history = lvObj.ContainsKey("scores") ? (GArray)lvObj["scores"] : new GArray();
		history.Add(score);
		lvObj["scores"] = history;

		SaveDoc(doc);
	}
}
