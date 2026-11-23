# Project Rules: AutoTtsStudio (C# WinForms)

## 1. Tech Stack & Môi trường
- Ngôn ngữ & Framework: C# WinForms (.NET 8 Windows, hoặc nâng lên .NET 9/10 nếu máy đã cài SDK tương ứng và thư viện yêu cầu; kiểm tra trước bằng `dotnet --list-sdks`).
- Thư viện cốt lõi: `System.Net.Http`, `System.Text.Json`, `NAudio`, gọi `ffmpeg.exe` / `ffprobe.exe` qua `System.Diagnostics.Process`.
- Không dùng WPF, Electron hay framework nặng không cần thiết.

## 2. Nguyên tắc Zero Bloat
- Không tạo unit test, mock data giả, nút bấm chết (không có handler) hoặc tính năng không được yêu cầu.
- Mọi control trên UI đều phải hoạt động thật. Nếu một tham số không hỗ trợ ở Engine hiện tại (VD: Stability ở Edge-TTS), hãy `Enabled = false` rõ ràng thay vì để bấm không có tác dụng.
- Giữ kiến trúc phẳng, rõ ràng, dễ bảo trì (`Models/`, `Providers/`, `Services/`, `Forms/`). Không lạm dụng DI container phức tạp hay pattern thừa thãi.

## 3. Tiêu chuẩn WinForms UI & Thread-Safety
- Thiết kế giao diện gọn, sạch, hỗ trợ DPI (`AutoScaleMode = AutoScaleMode.Dpi`), dùng `TableLayoutPanel`, `SplitContainer`, `Anchor`, `Dock` để không bao giờ bị cắt chữ hoặc đè control khi thay đổi kích thước cửa sổ.
- Font mặc định: `Segoe UI`, kích thước `9F`.
- Mọi thao tác mạng, gọi TTS, chạy FFmpeg đều phải là `async/await` chạy nền. Tuyệt đối không chặn UI thread.
- Cập nhật `DataGridView` và `Label` từ background thread bắt buộc dùng `IProgress<T>` hoặc `BeginInvoke`.
- Luôn `Dispose` stream âm thanh (`AudioFileReader`, `WaveOutEvent`) sau khi phát hoặc đọc thời lượng để không gây lỗi khóa file (`IOException`).

## 4. Quy tắc xác minh
- Trước khi kết thúc mỗi giai đoạn, bắt buộc chạy `dotnet build` để xác nhận biên dịch sạch 0 lỗi.
- Khi tích hợp TTS hoặc FFmpeg, phải kiểm tra thực tế bằng lệnh chạy thử hoặc hướng dẫn bước test ngắn gọn.