# AutoTTS Studio

Ứng dụng C# WinForms (.NET 9) chuyên dụng cho sản xuất âm thanh, video lồng tiếng hàng loạt với các engine TTS hàng đầu: **Edge-TTS (Miễn phí)**, **Local AI / Chatterbox TTS / Kokoro (OpenAI API)**, và **ElevenLabs API**.

---

## ✨ Tính Năng Nổi Bật

1. **Đa dạng Engine TTS:**
   - **Edge-TTS (100% Miễn phí):** Hỗ trợ đầy đủ giọng đọc chất lượng cao của Microsoft, tự động dự phòng qua cầu nối Node.js khi gặp mã bảo mật Sec-MS-GEC.
   - **Local AI (OpenAI API Compatible):** Tương thích hoàn toàn với Kokoro CPU/GPU và **Chatterbox TTS Server** (hỗ trợ kết nối Google Colab T4 GPU qua Cloudflare Tunnel). Tự động lấy danh sách 28 giọng clone và nhận diện định dạng âm thanh WAV/MP3.
   - **ElevenLabs API:** Tích hợp mô hình `eleven_multilingual_v2`, `eleven_turbo_v2_5` với tùy chỉnh chuyên sâu Stability, Similarity, Style Exaggeration, Speaker Boost.

2. **Xử lý Văn bản Thông minh (Smart Import & Split):**
   - Đọc và import file `.txt` và `.srt` (tự động nhận diện UTF-8 / UTF-8 BOM).
   - Nút **Dán văn bản** trực tiếp từ clipboard mà không cần tạo file trung gian.
   - Thuật toán ngắt câu thông minh: bảo vệ từ viết tắt tiếng Anh (`Mr.`, `Mrs.`, `Dr.`, `U.S.`, `a.m.`, `p.m.`...), dấu ba chấm `...`, tự động tách câu dài theo dấu phẩy mềm, không bao giờ ngắt ngang từ.

3. **Chạy hàng loạt Đa luồng & Proxy Rotation:**
   - Điều chỉnh số luồng song song (1–10 luồng).
   - Cơ chế Proxy Rotation (HTTP/HTTPS/SOCKS5) với tự động chuyển đổi proxy khi gặp lỗi mạng.
   - Tự động thử lại (Auto Retry) khi gặp gián đoạn kết nối.

4. **Trình phát âm thanh & Hậu kỳ Âm thanh (FFmpeg):**
   - Nghe thử trực tiếp từng dòng hoặc toàn bộ câu bằng **NAudio** không gây khóa file.
   - Tự động ghép nối toàn bộ danh sách file âm thanh thành 1 file MP3 duy nhất kèm khoảng nghỉ giữa câu tùy chỉnh (**Silence Gap**).
   - Tự động tạo file phụ đề `.srt` và timeline `[MM:SS]` theo thời lượng âm thanh thực tế.

5. **Giao diện Windows Hiện đại:**
   - Thiết kế chuẩn PerMonitorV2 High DPI, chống cắt chữ trên mọi độ phân giải laptop/màn hình rời.
   - Bảng hiển thị phụ đề với trạng thái màu sắc trực quan, tooltip báo chi tiết mã lỗi HTTP/server và menu chuột phải tiện lợi.

---

## 🚀 Hướng Dẫn Cài Đặt & Chạy

### Yêu Cầu Môi Trường
- **Hệ điều hành:** Windows 10 / 11 (x64)
- **.NET Runtime / SDK:** [.NET 9 SDK hoặc Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)
- **FFmpeg:** Cài đặt FFmpeg vào biến môi trường `PATH` hoặc tải `ffmpeg.exe` đặt vào thư mục `Tools/` (xem hướng dẫn tại [Tools/README.md](Tools/README.md)).
- **Node.js (Tùy chọn):** Dành cho cầu nối dự phòng Edge-TTS (`scripts/edge_tts_bridge.mjs`).

### Biên Dịch Từ Mã Nguồn
```bash
# Clone repository
git clone https://github.com/Danh-123/AutoTtsStudio.git
cd AutoTtsStudio

# Build dự án
dotnet build -c Release

# Xuất bản bản chạy độc lập
dotnet publish AutoTtsStudio/AutoTtsStudio.csproj -c Release -r win-x64 --self-contained false -o publish
```

---

## 📖 Hướng Dẫn Kết Nối Chatterbox TTS Trên Google Colab
Xem tài liệu hướng dẫn chi tiết từ A–Z tại [INSTRUCT.md](INSTRUCT.md).
