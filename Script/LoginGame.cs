using Godot;
using System;
using System.Collections.Generic;

public partial class LoginGame : Control
{
	// ตั้งผ่าน Inspector ได้หาก path ในฉากต่างไปจากตัวอย่าง
	[Export] private NodePath NameInputPath;
	[Export] private NodePath PasswordInputPath;
	[Export] private NodePath ErrorLabelPath;
	[Export] private NodePath SubmitButtonPath;

	private LineEdit _nameInput;
	private LineEdit _passwordInput;
	private Label _errorLabel;
	private Button _submitButton;

	// อักขระพิเศษที่ “อนุญาต” ในรหัสผ่าน
	private const string AllowedSpecials = @"!@#$%^&*()-_=+[]{};:'"",.<>/?\|`~";

	public override void _Ready()
	{
		_nameInput     = GetNodeOrNull<LineEdit>(NameInputPath)
					  ?? GetNodeOrNull<LineEdit>("CenterContainer/VBoxContainer/NameInput");

		_passwordInput = GetNodeOrNull<LineEdit>(PasswordInputPath)
					  ?? GetNodeOrNull<LineEdit>("CenterContainer/VBoxContainer/PasswordInput");

		_errorLabel    = GetNodeOrNull<Label>(ErrorLabelPath)
					  ?? GetNodeOrNull<Label>("CenterContainer/VBoxContainer/ErrorLabel");

		_submitButton  = GetNodeOrNull<Button>(SubmitButtonPath)
					  ?? GetNodeOrNull<Button>("CenterContainer/VBoxContainer/SubmitButton");

		if (_nameInput == null)     GD.PushError("NameInput not found.");
		if (_passwordInput == null) GD.PushError("PasswordInput not found.");
		if (_errorLabel == null)    GD.PushError("ErrorLabel not found.");
		if (_submitButton == null)  GD.PushError("SubmitButton not found.");

		if (_errorLabel != null) _errorLabel.Visible = false;
		if (_submitButton != null) _submitButton.Pressed += OnSubmit;

		if (_passwordInput != null) _passwordInput.Secret = true; // ซ่อนรหัส
	}

	private void OnSubmit()
	{
		if (_nameInput == null || _passwordInput == null || _errorLabel == null)
		{
			GD.PushError("UI nodes missing, cannot submit.");
			return;
		}

		// ===== ตรวจชื่อ =====
		string name = (_nameInput.Text ?? "").Trim();
		if (name.Length == 0)
		{
			ShowError("กรุณากรอกชื่ออย่างน้อย 1 ตัวอักษร");
			return;
		}
		string badName = GetInvalidNameChars(name);
		if (!string.IsNullOrEmpty(badName))
		{
			ShowError($"ไม่สามารถใช้ชื่อนี้ได้ เพราะมีตัวอักษรพิเศษ: {badName}");
			return;
		}

		// ===== ตรวจรหัสผ่าน =====
		string password = (_passwordInput.Text ?? "").Trim();
		string pwdErr = ValidatePassword(password);
		if (pwdErr != null)
		{
			ShowError(pwdErr);
			return;
		}

		// ===== บันทึกผ่าน PlayerLogin (ชื่อ + รหัส) =====
		var saver = GetNodeOrNull<PlayerLogin>("/root/PlayerLogin") ?? PlayerLogin.Instance;
		if (saver == null)
		{
			ShowError("ระบบ PlayerLogin (Autoload) ไม่ได้เปิดใช้งาน");
			GD.PushError("Missing /root/PlayerLogin. Add Autoload.");
			return;
		}

		if (!saver.SavePlayer(name, password))
		{
			ShowError("ชื่อนี้ถูกใช้ไปแล้ว หรือบันทึกข้อมูลไม่สำเร็จ");
			return;
		}

		HideError();
		GD.Print($"Login success with username: {name}");

		// ไปซีนถัดไป
		const string nextScene = "res://SceneStartandHigh/StartGame.tscn";
		if (ResourceLoader.Exists(nextScene))
			GetTree().ChangeSceneToFile(nextScene);
		else
			ShowError("ไม่พบซีนถัดไป: " + nextScene);
	}

	// ===== Helpers =====

	// อนุญาต A–Z a–z 0–9 และอักษรไทย
	private string GetInvalidNameChars(string s)
	{
		var list = new List<char>();
		foreach (char c in s)
		{
			if (!(char.IsLetterOrDigit(c) || (c >= 0x0E00 && c <= 0x0E7F)))
				if (!list.Contains(c)) list.Add(c);
		}
		return string.Join(", ", list);
	}

	// รหัสผ่านต้อง ≥ 8 ตัว และมีเฉพาะ a-z A-Z 0-9 หรืออักขระจาก AllowedSpecials
	private string ValidatePassword(string pwd)
	{
		if (string.IsNullOrEmpty(pwd) || pwd.Length < 8)
			return "รหัสผ่านต้องมีอย่างน้อย 8 ตัวอักษร";

		var invalids = new List<char>();
		foreach (char c in pwd)
		{
			if (char.IsLetterOrDigit(c)) continue;
			if (AllowedSpecials.IndexOf(c) >= 0) continue;
			if (!invalids.Contains(c)) invalids.Add(c);
		}
		if (invalids.Count > 0)
			return $"รหัสผ่านมีอักขระที่ไม่อนุญาต: {string.Join(", ", invalids)}";

		return null; // ผ่าน
	}

	private void ShowError(string msg)
	{
		_errorLabel.Text = msg;
		_errorLabel.Visible = true;
		_errorLabel.Modulate = new Color(1, 0, 0);
	}

	private void HideError()
	{
		_errorLabel.Text = "";
		_errorLabel.Visible = false;
	}
}
