using Godot;                                       
using System;                                      
 // คลาสปุ่มรูปตา สืบทอดจาก TextureButton 
public partial class EyeButton : TextureButton   
{
	[Export] public NodePath PasswordLineEditPath;  // [Export] ให้ลาก Node LineEdit ที่เป็นช่องรหัสผ่านมาผูกใน Inspector
	[Export] public Texture2D Icon;  // [Export] รูปภาพที่จะใช้เป็นไอคอนตา (ใช้รูปเดียวทุกสถานะ)
	[Export] public bool StartClosed = true;  // [Export] ถ้า true จะเริ่มต้นด้วยการ "ซ่อนรหัส" (LineEdit.Secret = true)

	
	[Export(PropertyHint.Range, "0,1,0.05")] public float HiddenOpacity = 0.6f; 
	// [Export] ตัวเลข 0–1 (แสดงเป็น slider) ใช้กำหนดความทึบเมื่อซ่อนรหัส 0.6 = จางหน่อย

	private LineEdit _line;  // ตัวแปรเก็บอ้างอิงช่องรหัสผ่าน (LineEdit)
	private bool _hidden; // เก็บสถานะว่าขณะนี้ "ซ่อนรหัสอยู่หรือไม่"
	
	// เรียกเมื่อ Node พร้อมใช้งาน (หลังโหลดเข้าฉาก)
	public override void _Ready()                  
	{
		// พยายามดึง LineEdit ตาม path ที่ตั้งไว้ใน Inspector
		_line = GetNodeOrNull<LineEdit>(PasswordLineEditPath); 
		if (_line == null)   // ถ้าไม่พบ
		{
			GD.PushError("[EyeButton] Assign PasswordLineEditPath to a LineEdit."); 
			return;  // หยุดไม่ทำงานต่อ
		}

		//  ตั้งค่าปุ่ม 
		StretchMode = StretchModeEnum.Scale;  // ให้ภาพขยาย/ย่อพอดีกับขนาดปุ่ม
		IgnoreTextureSize = true; // ไม่ใช้ขนาดเดิมของ texture เป็นตัวกำหนดขนาดปุ่ม
		TextureFilter = CanvasItem.TextureFilterEnum.Linear; // ทำให้ขอบภาพเนียนขึ้น (ไม่หยัก)

		// ตั้งค่าขนาดปุ่ม 
		CustomMinimumSize = new Vector2(20, 20);    // ขนาด 20×20 พิกเซล 
		SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;   // จัดแนวให้หดไปทางขวาสุดของ container
		SizeFlagsVertical   = Control.SizeFlags.ShrinkCenter; // จัดแนวให้อยู่กลางแนวดิ่ง

		if (Icon != null)  // ถ้ามีภาพที่กำหนดไว้
			TextureNormal = TextureHover = TexturePressed = TextureDisabled = Icon; 
			// ใช้ภาพเดียวกันทุกสถานะ (ปกติ, ชี้, กด, ปิดการใช้งาน)

		_hidden = StartClosed; // ตั้งสถานะเริ่มต้นให้ตรงกับค่าที่ Export
		Apply();   // เรียกใช้ Apply() เพื่ออัปเดตค่าจริง (Secret, ความจาง ฯลฯ)
		
		// ผูกสัญญาณกดปุ่ม (Pressed) → เรียกฟังก์ชัน OnPressed
		Pressed += OnPressed; 
	}
	// เรียกเมื่อผู้ใช้ "คลิกปุ่มตา"
	private void OnPressed()   
	{
		int caret = _line.CaretColumn;  // เก็บตำแหน่งเคอร์เซอร์ในช่องไว้ (เพื่อไม่ให้เคลื่อน)
		_hidden = !_hidden;  // สลับสถานะซ่อน/โชว์ (toggle true ↔ false)
		Apply();   // เรียก Apply() เพื่ออัปเดตค่าในช่องรหัส
		_line.CaretColumn = caret;  // คืนตำแหน่งเคอร์เซอร์เดิม
		_line.GrabFocus();  // ให้ช่องรหัสได้โฟกัสกลับ (ผู้ใช้พิมพ์ต่อได้ทันที)
	}
	// ฟังก์ชันหลักที่อัปเดตสถานะของช่องรหัสและปุ่ม
	private void Apply()                         
	{
		_line.Secret = _hidden;  // ถ้า _hidden = true → ซ่อนรหัส (เป็น •••), ถ้า false → โชว์ข้อความ
		TooltipText = _hidden ? "Show password" : "Hide password"; // ปรับ tooltip ตามสถานะ

		// ปรับความทึบของปุ่มเพื่อบอกสถานะ (ถ้าอยากให้เหมือนเดิมทุกสถานะ ให้ HiddenOpacity = 1)
		SelfModulate = _hidden  // ถ้าอยู่ในสถานะซ่อน
			? new Color(1, 1, 1, HiddenOpacity) // ทำให้จางตามค่า HiddenOpacity
			: Colors.White;  // ถ้าโชว์รหัส → สีปกติ (ทึบ 100%)
	}
}
