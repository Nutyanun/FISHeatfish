using Godot;                                     
using System;                                     
using System.Linq;                                
using System.Collections.Generic;                 											 
using GDict = Godot.Collections.Dictionary;  // ชื่อย่อให้ Godot.Collections.Dictionary (เป็น dynamic map ของ Godot)
using GArray = Godot.Collections.Array;   // ชื่อย่อให้ Godot.Collections.Array (เป็น dynamic array ของ Godot)

public static class LeaderboardStore // คลาสสาธารณะสำหรับจัดการข้อมูล Leaderboard และ Players (แบบ static utility)
{
	public static string PlayersPath => "user://players.json"; // พาธไฟล์ข้อมูลในโฟลเดอร์ user:// (เขียนได้)

	//สร้างคีย์วันที่วันนี้รูปแบบ YYYY-MM-DD (ตามเวลาเครื่อง)
	public static string MakeTodayKeyLocal() => DateTime.Now.ToString("yyyy-MM-dd"); // สร้างคีย์วันที่วันนี้รูปแบบ YYYY-MM-DD (ตามเวลาเครื่อง)
	
	 // รูปแบบวันที่เพื่อโชว์เป็นหัวข้อ (ไทย)
	public static string FormatThaiDateForHeader(string yyyyMmDd)   
	{
		if (DateTime.TryParse(yyyyMmDd, out var d))   // พยายาม parse สตริงวันที่
			return d.ToString("dd/MM/yyyy", new System.Globalization.CultureInfo("th-TH")); // ถ้าได้: แสดง dd/MM/yyyy ตามวัฒนธรรมไทย
		return yyyyMmDd; // ถ้า parse ไม่ได้: คืนค่าเดิม
	}

	// โหลดเอกสาร (players.json) เป็น Dictionary ของ Godot
	public static GDict LoadDoc()    
	{
		if (!FileAccess.FileExists(PlayersPath)) return new GDict();   // ถ้าไฟล์ยังไม่มี ให้คืน dict ว่าง
		using var f = FileAccess.Open(PlayersPath, FileAccess.ModeFlags.Read);  // เปิดไฟล์ในโหมดอ่าน
		var v = Json.ParseString(f.GetAsText());  // อ่านทั้งหมดเป็นสตริง แล้ว parse JSON เป็น Variant
		return v.VariantType == Variant.Type.Dictionary ? (GDict)v : new GDict();  // ถ้าเป็น Dictionary ก็แคสต์คืน มิฉะนั้นคืน dict ว่าง
	}
   // บันทึกเอกสารกลับลงไฟล์
	public static void SaveDoc(GDict doc)  
	{
		using var f = FileAccess.Open(PlayersPath, FileAccess.ModeFlags.Write);  // เปิดไฟล์ในโหมดเขียน (ทับ)
		f.StoreString(Json.Stringify(doc, "\t"));  // แปลง Dictionary เป็น JSON พร้อมแท็บให้อ่านง่าย แล้วเขียนลงไฟล์
	}

	//แน่ใจว่ารูทมีคีย์ "players" และ "leaderboards"
	public static GDict EnsureRoot(GDict doc) 
	{
		if (!doc.ContainsKey("players"))      doc["players"] = new GDict(); // ถ้าไม่มี "players" ให้สร้าง dict ว่าง
		if (!doc.ContainsKey("leaderboards")) doc["leaderboards"] = new GDict(); // ถ้าไม่มี "leaderboards" ให้สร้าง dict ว่าง
		return doc;   // คืนเอกสารที่ถูกเติมโหนดขั้นต่ำแล้ว
	}
   
   // แน่ใจว่ามีโหนดของวันที่ใน leaderboards
	public static GDict EnsureLeaderboardDate(GDict doc, string dateKey) 
	{
		var lb = (GDict)doc["leaderboards"];  // เข้าถึง dict ของ leaderboards
		if (!lb.ContainsKey(dateKey)) lb[dateKey] = new GDict();  // ถ้ายังไม่มีคีย์วันที่นี้ ให้สร้าง dict ว่าง
		return (GDict)lb[dateKey];  // คืน dict ของวันที่นั้น
	}
   
	// แน่ใจว่ามีอาร์เรย์ของเลเวลในวันที่นั้น
	public static GArray EnsureLeaderboardLevel(GDict dateNode, string levelKey)
	{
		if (!dateNode.ContainsKey(levelKey)) dateNode[levelKey] = new GArray(); // ถ้ายังไม่มีคีย์ของเลเวลนี้ ให้สร้าง array ว่าง
		return (GArray)dateNode[levelKey];  // คืนอาร์เรย์ของเลเวลนี้
	}

	// เพิ่ม/อัปเดตสกอร์ลงลีดเดอร์บอร์ด
	public static void UpsertScore(string dateKey, int levelIndex, string playerName, int score, int limitTop = 50) 
	{
		var doc = EnsureRoot(LoadDoc()); // โหลดไฟล์ แล้ว ensure โครงสร้างรูทครบ

		// 1) leaderboards
		var dateNode = EnsureLeaderboardDate(doc, dateKey); // ensure โหนดวันที่ใน leaderboards
		var levelKey = levelIndex.ToString();  // แปลงเลเวลเป็นสตริงใช้เป็นคีย์
		var arr = EnsureLeaderboardLevel(dateNode, levelKey);  // ensure อาร์เรย์ของเลเวลนี้

		int foundIdx = -1;   // เก็บตำแหน่งของผู้เล่นในอาร์เรย์ ถ้ายังไม่พบ = -1
		for (int i = 0; i < arr.Count; i++)  // วนหาในอาร์เรย์ของเลเวลนี้
		{
			var row = (GDict)arr[i];   // แคสต์สมาชิกเป็น Dictionary (หนึ่งแถว)
			var name = row.ContainsKey("name") ? row["name"].AsString() : "";   // อ่านชื่อ ถ้าไม่มีให้เป็น ""
			if (name == playerName) { foundIdx = i; break; }   // ถ้าชื่อซ้ำกัน ถือว่าเจอ → จด index แล้วหยุด
		}

		var nowIso = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");  // เวลาปัจจุบันในฟอร์แมต ISO (มี timezone)

		if (foundIdx >= 0)  // ถ้ามีผู้เล่นคนนี้อยู่แล้ว
		{
			var row = (GDict)arr[foundIdx];   // ดึงแถวเดิม
			int old = row.ContainsKey("score") ? (int)(long)row["score"] : 0;  // คะแนนเดิม (ข้อมูล JSON อ่านเป็น long → แคสต์เป็น int)
			if (score > old)   // ถ้าคะแนนใหม่ดีกว่าเดิม
			{
				row["score"] = score;  // อัปเดตคะแนน
				row["updated_at"] = nowIso;  // อัปเดตเวลาแก้ไข
				arr[foundIdx] = row;  // เขียนกลับเข้าอาร์เรย์
			}
		}
		else   // ถ้าเป็นผู้เล่นใหม่สำหรับเลเวลนี้
		{
			var row = new GDict { // สร้างแถวใหม่ 
				{ "name", playerName },  // เก็บชื่อ
				{ "score", score },  // เก็บคะแนน
				{ "updated_at", nowIso }   // เวลาอัปเดต
			};
			arr.Add(row); // เพิ่มเข้าอาร์เรย์ของเลเวลนี้
		}

		// sort: score desc, tie -> updated_at asc
		var list = new List<GDict>();  // ใช้ List<GDict> เพื่อเรียงลำดับสะดวก
		foreach (var v in arr) list.Add((GDict)v);  // คัดลอกจาก GArray → List<GDict>

		list.Sort((a, b) =>  // นิยามรูปแบบการเรียงลำดับ
		{
			int sa = (int)(long)a["score"]; // คะแนน a (long → int)
			int sb = (int)(long)b["score"];  // คะแนน b (long → int)
			int cmp = sb.CompareTo(sa); // เรียงคะแนนจากมากไปน้อย 
			if (cmp != 0) return cmp;  // ถ้าไม่เท่ากัน จบที่คะแนน

			var ua = a.ContainsKey("updated_at") ? a["updated_at"].AsString() : "";  // เวลาอัปเดตของ a
			var ub = b.ContainsKey("updated_at") ? b["updated_at"].AsString() : "";  // เวลาอัปเดตของ b
			return string.Compare(ua, ub, StringComparison.Ordinal);  // ถ้าคะแนนเท่ากัน ให้เรียงเวลาจากเก่าไปใหม่ (asc)
		});

		if (limitTop > 0 && list.Count > limitTop)   // ถ้ากำหนดลิมิตจำนวนอันดับสูงสุด
			list = list.Take(limitTop).ToList();  // ตัดให้เหลือเท่าที่กำหนด

		var newArr = new GArray();// สร้าง GArray ใหม่
		foreach (var r in list) newArr.Add(r);   // เติมข้อมูลที่เรียงแล้วกลับเข้า GArray
		dateNode[levelKey] = newArr;  // เขียนทับอาร์เรย์เก่าในโหนดวันที่/เลเวล

		// 2) players: update personal high + history
		var players = (GDict)doc["players"];  // เข้าถึง dict "players"
		if (!players.ContainsKey(playerName))   // ถ้าใน players ยังไม่มีคนนี้
		{
			players[playerName] = new GDict {   // สร้างโปรไฟล์ผู้เล่น
				{ "registered_at", nowIso },  // เวลาเริ่มถูกบันทึก 
				{ "levels", new GDict() }  // เตรียม dict สำหรับเก็บข้อมูลรายเลเวล
			};
		}
		var pObj = (GDict)players[playerName]; // ดึงโปรไฟล์ผู้เล่น
		if (!pObj.ContainsKey("levels")) pObj["levels"] = new GDict();  // เผื่อกรณีไฟล์เก่าไม่มีคีย์ "levels"
		var levels = (GDict)pObj["levels"];   // dict ของข้อมูลรายเลเวล
		if (!levels.ContainsKey(levelKey))   // ถ้ายังไม่มีเลเวลนี้ในโปรไฟล์
		{
			levels[levelKey] = new GDict {   // สร้างข้อมูลเริ่มต้นของเลเวล
				{ "high", 0 },   // ค่า high score เริ่มที่ 0
				{ "scores", new GArray() } // ประวัติคะแนน (อาร์เรย์)
			};
		}
		var lvObj = (GDict)levels[levelKey];  // ดึงอ็อบเจ็กต์ของเลเวล
		int oldHigh = lvObj.ContainsKey("high") ? (int)(long)lvObj["high"] : 0; // อ่านค่า high เดิม
		if (score > oldHigh) lvObj["high"] = score; // ถ้าคะแนนใหม่สูงกว่า ให้ตั้งเป็น high ใหม่

		var history = lvObj.ContainsKey("scores") ? (GArray)lvObj["scores"] : new GArray(); // ดึงอาร์เรย์ประวัติคะแนน (หรือสร้างใหม่)
		history.Add(score);   // เพิ่มคะแนนล่าสุดเข้า history
		lvObj["scores"] = history;   // เขียนกลับเข้าอ็อบเจ็กต์เลเวล
		
		// บันทึก current_level ของผู้เล่นให้ตรงกับเลเวลที่เล่นล่าสุด
		try 
		{
			int currentLevel = 1; // สร้างตัวแปร currentLevel เริ่มต้นเป็น 1 (ใช้เมื่อไม่มีข้อมูลในไฟล์)

			// ถ้ามีคีย์ current_level อยู่ใน pObj แล้ว ให้อ่านค่าเดิมจากไฟล์
			if (pObj.ContainsKey("current_level")) // ตรวจว่ามี key ชื่อ "current_level" หรือไม่
			currentLevel = (int)(long)pObj["current_level"]; // แปลงค่าจาก Variant -> long -> int แล้วเก็บไว้ใน currentLevel

			// ถ้าเล่นผ่านด่านนี้แล้ว และด่านที่เพิ่งเคลียร์มากกว่าค่าปัจจุบัน → ให้อัปเดต current_level
			if (GameProgress.IsLevelCleared && levelIndex + 1 > currentLevel) // ตรวจว่าเคลียร์ด่านแล้วและด่านนี้สูงกว่า current_level เดิม
			{
				pObj["current_level"] = levelIndex; // บันทึกค่าด่านล่าสุดลงใน key "current_level"
				GD.Print($"[LeaderboardStore] * Updated {playerName} current_level = {levelIndex}"); // แสดงข้อความใน Output เพื่อดีบักว่ามีการอัปเดต
			}
			else 
			{
				GD.Print($"[LeaderboardStore]  current_level unchanged ({currentLevel})"); 
			}
		}
		catch (Exception ex) 
		{
			GD.PushWarning($"[LeaderboardStore] Failed to update current_level: {ex.Message}");
		}
		
		SaveDoc(doc); // บันทึกเอกสารทั้งหมดกลับลงไฟล์
	}
}
