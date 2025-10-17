using Godot;   // ใช้คลาสพื้นฐานของ Godot (เช่น Node, Vector2 ฯลฯ)
using System;  // ใช้ฟีเจอร์พื้นฐานของ .NET (เช่น enum, attribute)

// res://scripts/CrystalType.cs
// ไฟล์นี้เก็บ enum ที่ใช้ระบุ “ชนิดของคริสตัล” เพื่อให้เรียกใช้ง่ายและปลอดภัย

// ประกาศ enum เป็น public เพื่อให้ export จาก Inspector ได้ (เช่น [Export] public CrystalType Type)
public enum CrystalType 
{
	Blue = 0,    // คริสตัลสีน้ำเงิน — อาจเพิ่มพลังพื้นฐาน
	Red = 1,     // คริสตัลสีแดง — ใช้เพิ่มหรือลดเวลา (แล้วแต่โหมด)
	Green = 2,   // คริสตัลสีเขียว — เพิ่ม stack พลังป้องกัน / บัฟถาวร
	Pink = 3,    // คริสตัลสีชมพู — ปล่อยเฉพาะช่วงท้ายเกม (โบนัสพิเศษ)
	Purple = 4,  // คริสตัลสีม่วง — บัฟคูณคะแนนชั่วคราว
}
