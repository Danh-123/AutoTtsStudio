# FFmpeg for AutoTtsStudio

Thư mục này chứa `ffmpeg.exe` phục vụ cho việc:
- Đọc thời lượng chính xác của file âm thanh (.mp3, .wav).
- Ghép nối hàng loạt file âm thanh thành 1 file MP3 duy nhất kèm khoảng nghỉ giữa câu (Silence Gap).

Nếu bạn clone repository mà chưa có file `ffmpeg.exe`:
1. Tải bản build Windows của FFmpeg từ:
   - [FFmpeg Official Download](https://ffmpeg.org/download.html)
   - Hoặc [BtbN FFmpeg Builds Releases](https://github.com/BtbN/FFmpeg-Builds/releases) (chọn file `ffmpeg-master-latest-win64-gpl.zip`)
2. Giải nén và copy file `ffmpeg.exe` vào thư mục `Tools/` này.
3. Hoặc bạn có thể cài đặt FFmpeg vào biến môi trường `PATH` của máy tính.
