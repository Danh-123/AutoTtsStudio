# Hướng Dẫn A–Z: Chạy Chatterbox TTS Server Trên Google Colab & Kết Nối AutoTTS Studio

Tài liệu này đã tích hợp sẵn **toàn bộ bản vá lỗi** (xung đột `torchvision` trên Python 3.13, lỗi clone lồng thư mục, lỗi `Float vs Double` ở `s3tokenizer`, `voice_encoder` và `tts_turbo`). Bạn chỉ cần chạy đúng **2 ô code** là hệ thống sẵn sàng.

---

## Phần 1: Chuẩn bị Google Colab (Bắt buộc)

1. Truy cập [colab.research.google.com](https://colab.research.google.com) và tạo một Notebook mới (**New notebook**).
2. Trên thanh menu phía trên, chọn **Runtime (Thời gian chạy)** -> **Change runtime type (Thay đổi loại thời gian chạy)**.
3. Ở mục **Hardware accelerator**, chọn **`T4 GPU`** -> bấm **Save (Lưu)**.
4. Đổi tên Notebook (VD: `Chatterbox_AutoTTS_Server.ipynb`) để lần sau mở lại dùng ngay mà không cần copy lại code.

---

## Phần 2: Chạy Code trên Google Colab

### Ô 1: Cài đặt môi trường & Tự động vá toàn bộ lỗi (Chạy 1 lần mỗi phiên Colab)

Copy toàn bộ đoạn code dưới đây dán vào **Ô 1** và bấm nút **Play (▶)**. Quá trình cài mất khoảng **1–2 phút**.

```python
import os, subprocess, sys, torch

# 0. Kiểm tra bắt buộc phải có GPU trước khi cài
if not torch.cuda.is_available():
    raise RuntimeError("CHƯA BẬT GPU! Hãy vào Runtime -> Change runtime type -> chọn T4 GPU rồi chạy lại Ô 1.")

print(f"[1/4] Đã nhận GPU: {torch.cuda.get_device_name(0)} | Python: {sys.version.split()[0]}")

# 1. Luôn về thư mục /content và clone sạch (tránh lỗi lồng thư mục)
%cd /content
!rm -rf /content/Chatterbox-TTS-Server
!git clone -q https://github.com/devnen/Chatterbox-TTS-Server.git
%cd /content/Chatterbox-TTS-Server

# 2. Lọc bỏ các dòng ép cài torch/torchvision/torchaudio cũ để dùng PyTorch CUDA sẵn có của Colab
print("[2/4] Đang cài đặt thư viện tương thích Colab...")
with open("requirements-nvidia.txt", "r", encoding="utf-8") as f:
    lines = f.readlines()

skip_prefixes = ("torch", "torchvision", "torchaudio", "--index-url", "--extra-index-url")
clean_lines = []
chatterbox_pkg = "chatterbox-tts"

for line in lines:
    raw = line.split("#")[0].strip()
    low = raw.lower()
    if not low:
        continue
    if "chatterbox" in low:
        chatterbox_pkg = raw
        continue
    if low.startswith(skip_prefixes):
        continue
    clean_lines.append(raw + "\n")

with open("req_colab.txt", "w", encoding="utf-8") as f:
    f.writelines(clean_lines)

!pip install -q -r req_colab.txt
subprocess.check_call(["pip", "install", "-q", "--no-deps", chatterbox_pkg])
!pip install -q s3tokenizer resemble-perth conformer diffusers transformers safetensors omegaconf librosa pydub einops

# 3. Tải Cloudflare Tunnel
!wget -q https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64 -O cloudflared
!chmod +x cloudflared

# 4. Tự động định vị thư mục chatterbox và vá toàn bộ lỗi Float/Double (float64 -> float32)
print("[3/4] Đang áp dụng các bản vá lỗi Float32...")
import chatterbox
pkg_dir = os.path.dirname(chatterbox.__file__)

def patch_file(rel_path, replacements):
    full_path = os.path.join(pkg_dir, rel_path)
    if not os.path.exists(full_path):
        print(f"  [Cảnh báo] Không thấy file: {full_path}")
        return
    with open(full_path, "r", encoding="utf-8") as f:
        code = f.read()
    changed = False
    for old_str, new_str in replacements:
        if old_str in code:
            code = code.replace(old_str, new_str)
            changed = True
    if changed:
        with open(full_path, "w", encoding="utf-8") as f:
            f.write(code)
        print(f"  [OK] Đã vá: {rel_path}")
    else:
        print(f"  [OK] Không cần vá hoặc đã vá sẵn: {rel_path}")

# Vá 1: s3tokenizer.py (RuntimeError: expected scalar type Float but found Double)
patch_file(
    os.path.join("models", "s3tokenizer", "s3tokenizer.py"),
    [(
        "mel_spec = self._mel_filters.to(self.device) @ magnitudes",
        "mel_spec = self._mel_filters.to(self.device).float() @ magnitudes.float()"
    )]
)

# Vá 2: voice_encoder.py (ValueError: RNN input dtype float64 does not match float32)
patch_file(
    os.path.join("models", "voice_encoder", "voice_encoder.py"),
    [
        (
            "_, (hidden, _) = self.lstm(mels)",
            "_, (hidden, _) = self.lstm(mels.float())"
        ),
        (
            "self.inference(mels.to(self.device)",
            "self.inference(mels.to(self.device, dtype=torch.float32)"
        )
    ]
)

# Vá 3: tts_turbo.py (ép ref_16k_wav & ve_embed về float32)
patch_file(
    "tts_turbo.py",
    [
        (
            "s3_tokzr.forward([ref_16k_wav[:self.ENC_COND_LEN]], max_len=plen)",
            "s3_tokzr.forward([ref_16k_wav[:self.ENC_COND_LEN].float() if hasattr(ref_16k_wav, 'float') else ref_16k_wav[:self.ENC_COND_LEN].astype('float32')], max_len=plen)"
        ),
        (
            "ve_embed = torch.from_numpy(self.ve.embeds_from_wavs([ref_16k_wav], sample_rate=S3_SR))",
            've_embed = torch.from_numpy(self.ve.embeds_from_wavs([ref_16k_wav.astype("float32") if hasattr(ref_16k_wav, "astype") else ref_16k_wav.float()], sample_rate=S3_SR)).float()'
        )
    ]
)

# 5. Xác nhận cuối cùng
from chatterbox.tts import ChatterboxTTS
print("\n========================================================")
print("[4/4] CÀI ĐẶT & VÁ LỖI HOÀN TẤT! CUDA:", torch.cuda.is_available())
print("Hãy chạy tiếp Ô 2 bên dưới để khởi động server.")
print("========================================================")
```

**Dấu hiệu thành công ở Ô 1:** Cuối ô hiện dòng **`[4/4] CÀI ĐẶT & VÁ LỖI HOÀN TẤT! CUDA: True`**.

---

### Ô 2: Khởi động Chatterbox Server & Lấy Link API

Tạo ô code thứ 2, dán đoạn sau và bấm **Play (▶)**. Lần đầu chạy sẽ tải model (~2GB) mất khoảng **1–2 phút**.

```python
%cd /content/Chatterbox-TTS-Server
import subprocess, threading, time, re

def run_tunnel():
    time.sleep(12)
    p = subprocess.Popen(
        ["./cloudflared", "tunnel", "--url", "http://localhost:8004"],
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True
    )
    for line in p.stdout:
        m = re.search(r"https://[a-zA-Z0-9-]+\.trycloudflare\.com", line)
        if m:
            base_url = m.group(0)
            print("\n" + "=" * 62)
            print("SERVER ĐÃ SẴN SÀNG! COPY LINK DƯỚI ĐÂY:")
            print(f"1. Mở trình duyệt quản lý/upload giọng mẫu:\n   {base_url}")
            print(f"2. Dán vào ô 'Local API URL' trong AutoTTS Studio:\n   {base_url}/v1/audio/speech")
            print("=" * 62 + "\n")

threading.Thread(target=run_tunnel, daemon=True).start()
!python server.py
```

**Dấu hiệu thành công ở Ô 2:**
1. Xuất hiện khung đường link `https://<chuoi-ngau-nhien>.trycloudflare.com`.
2. Log bên dưới hiện `INFO: Application startup complete.` và `Uvicorn running on http://0.0.0.0:8004`.
*(Lưu ý: **Giữ Ô 2 luôn chạy** trong suốt thời gian dùng tool C#, không bấm Stop.)*

---

## Phần 3: Cấu hình trên Web Server (Làm nhanh trong 30 giây)

1. Mở **Link số 1** (`https://....trycloudflare.com`) trên trình duyệt.
2. Kiểm tra góc trên phải hiện chấm xanh **`ChatterboxTurboTTS loaded on cuda`**.
3. **Chọn hoặc tải giọng clone:**
   * Dùng giọng dựng sẵn trong tab **`Predefined Voices`** (gồm 28 giọng tiếng Anh như `Eli`, `Emily`, `Michael`, `Alexander`, `Adrian`...).
   * Hoặc bấm nút **`Import`** cạnh ô `Select Predefined Voice` để tải file `.wav` giọng mẫu (~10 giây, sạch tiếng ồn) lên rồi bấm **`Refresh`**.
4. **Cố định tông giọng xuyên suốt video dài (mục `Generation Parameters`):**
   * **Temperature:** `0.7` – `0.75` (giữ nhịp kể ổn định, không bị AI đọc lố).
   * **Speed Factor:** `1` (hoặc `0.95` cho phim tài liệu trầm ấm).
   * **Generation Seed:** Đặt cố định một số dương (VD: **`3000`**) để mọi câu nối tiếp giữ cùng một sắc thái giọng.
   * **Output Format:** Giữ **`WAV`**.
   * Bấm **`Save Generation Parameters`**.
5. **Test nhanh 1 câu:** Bấm **`Generate Speech`** trên web, nghe phát ra tiếng rõ ràng là server đã thông 100%.

---

## Phần 4: Kết nối & Chạy trên AutoTTS Studio

1. Mở **AutoTTS Studio** trên Windows, kiểm tra thanh dưới cùng báo **`FFmpeg: Sẵn sàng`**.
2. Chọn **Engine:** `Local AI (OpenAI API)`.
3. Bấm **`Cài đặt API / FFmpeg`**:
   * **Local API URL:** Dán **Link số 2** (`https://....trycloudflare.com/v1/audio/speech`), kiểm tra kỹ có đủ `https://` và không dính khoảng trắng thừa.
   * **Local API Model:** `chatterbox`
   * **Local API Key:** Để trống.
   * Bấm **`Lưu`**.
4. **Chọn giọng & thông số chạy tối ưu:**
   * Ô **`Voice:`** tự động tải danh sách 28 giọng từ Colab -> chọn giọng bạn muốn (VD: `Eli (Eli.wav)` hoặc giọng bạn vừa import).
   * Cụm **`Options`:**
     * Bật **`Auto Retry (lỗi mạng)`** và **`Auto Split câu dài`**.
     * **`Số luồng`:** Bắt buộc đặt **`1`** (tránh tràn VRAM trên T4 GPU).
     * **`Max ký tự`:** **`240` – `280`** (vùng tối ưu của Chatterbox Turbo).
     * **`Nghỉ giữa câu (ms)`:** **`300` – `350`**.
5. **Chạy kịch bản & Xuất thành phẩm:**
   * Bấm **`Import File`** (hoặc **`Dán văn bản`**) -> bấm **`Start`**.
   * Nên bấm **`Stop`** sau 3–4 dòng đầu để **`Nghe thử`** tông giọng; nếu ưng ý bấm **`Start`** lại để tool tự chạy tiếp các dòng còn lại.
   * Chạy xong bấm **`Join MP3`** -> **`Tạo SRT`** (hoặc **`Timeline [MM:SS]`**) -> **`Open Output`**.
   * Nếu kịch bản có chèn thẻ cảm xúc của Chatterbox Turbo (`[sigh]`, `[chuckle]`, `[gasp]`, `[laugh]`...), nhớ mở file `.srt` bằng Notepad (`Ctrl + H`) xóa các thẻ này trước khi đưa vào CapCut/Premiere.

---

## Phần 5: Xử lý nhanh khi gặp sự cố

* **Colab bị ngắt giữa chừng (sau ~1–2 tiếng hoặc do để máy ngủ):**
  * **KHÔNG tắt AutoTTS Studio.**
  * Nếu Colab vẫn giữ runtime (chưa reset máy ảo): chỉ cần chạy lại **Ô 2** lấy link Cloudflare mới.
  * Nếu Colab báo *Runtime disconnected* (reset máy ảo): chạy lại **Ô 1** rồi **Ô 2** (nhớ import lại giọng clone nếu có dùng file riêng).
  * Dán link `/v1/audio/speech` mới vào **`Cài đặt API / FFmpeg`** -> bấm **`Lưu`** -> bấm **`Start`**. Tool sẽ tự bỏ qua các dòng đã **`Done`** và chạy nối tiếp đúng từ chỗ bị dừng.
* **Gặp cảnh báo `FutureWarning` hoặc `UnsupportedFieldAttributeWarning` màu trắng/vàng trong log Ô 2:** Đây chỉ là cảnh báo phiên bản của `diffusers` và `pydantic`, hoàn toàn vô hại — bỏ qua.
* **Một vài câu chứa thuật ngữ khó đọc bị vấp:** Click chuột phải vào đúng dòng đó trên bảng -> **`Sửa nội dung`** (thêm dấu phẩy hoặc viết phiên âm gần đúng) -> chuột phải chọn **`Tạo lại dòng này`**.