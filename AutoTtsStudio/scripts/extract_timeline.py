#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
extract_timeline.py - Bóc Timeline chính xác theo câu bằng faster-whisper.
In ra stdout DUY NHẤT một chuỗi JSON UTF-8 dạng:
[{"start": 0.0, "end": 3.85, "text": "..."}]
Mọi log tiến trình được in ra sys.stderr.
"""

import sys
import os
import json
import re

ABBREVIATIONS = {
    "mr.", "mrs.", "ms.", "dr.", "prof.", "st.", "u.s.", "u.s.a.",
    "a.m.", "p.m.", "vs.", "etc.", "i.e.", "e.g.", "inc.", "corp.", "co.", "jr.", "sr."
}

def log(msg: str):
    sys.stderr.write(f"[TimelineExtractor] {msg}\n")
    sys.stderr.flush()

def main():
    if len(sys.argv) < 2:
        log("Sử dụng: python extract_timeline.py <duong_dan_file_am_thanh>")
        sys.exit(1)

    audio_path = sys.argv[1]
    if not os.path.exists(audio_path):
        log(f"Lỗi: Không tìm thấy file âm thanh tại '{audio_path}'")
        sys.exit(2)

    try:
        from faster_whisper import WhisperModel
    except ImportError:
        log("LỖI: Thiếu thư viện faster-whisper! Vui lòng chạy lệnh: pip install faster-whisper")
        sys.exit(3)

    log("Đang nạp mô hình Whisper (base.en, cpu, int8)...")
    try:
        model = WhisperModel("base.en", device="cpu", compute_type="int8")
    except Exception as e:
        log(f"Lỗi khởi tạo WhisperModel: {e}")
        sys.exit(4)

    log(f"Bắt đầu nhận diện âm thanh: {os.path.basename(audio_path)}")
    try:
        segments, info = model.transcribe(
            audio_path,
            beam_size=5,
            word_timestamps=True,
            vad_filter=True,
            vad_parameters=dict(min_silence_duration_ms=400)
        )
    except Exception as e:
        log(f"Lỗi khi transcribe file: {e}")
        sys.exit(5)

    all_words = []
    for segment in segments:
        if segment.words:
            for w in segment.words:
                all_words.append(w)
        else:
            all_words.append(type('Word', (), {
                'start': segment.start,
                'end': segment.end,
                'word': segment.text.strip()
            })())

    if not all_words:
        log("Cảnh báo: Không nhận diện được từ nào trong file âm thanh.")
        sys.stdout.reconfigure(encoding='utf-8')
        print("[]")
        return

    sentences = []
    current_words = []
    current_start = None

    for i, w in enumerate(all_words):
        word_text = w.word.strip()
        if not word_text:
            continue

        if current_start is None:
            current_start = w.start

        current_words.append(word_text)
        current_end = w.end

        clean_lower = word_text.lower()
        is_end_punct = bool(re.search(r'[.!?]["\']?$', word_text))
        is_decimal = bool(re.search(r'\d+\.\d+$', word_text))
        is_abbr = clean_lower in ABBREVIATIONS or bool(re.match(r'^(?:[a-zA-Z]\.){2,}$', clean_lower))

        is_last_word = (i == len(all_words) - 1)
        next_is_capital = False
        if not is_last_word:
            next_word = all_words[i + 1].word.strip()
            if next_word:
                first_char = next_word[0]
                next_is_capital = first_char.isupper() or first_char.isdigit()

        should_split = False
        if is_last_word:
            should_split = True
        elif is_end_punct and not is_decimal and not is_abbr and next_is_capital:
            should_split = True

        if should_split and current_words:
            sentence_text = " ".join(current_words)
            duration = current_end - current_start

            if duration < 1.0 and sentences:
                prev = sentences[-1]
                prev["end"] = round(current_end, 3)
                prev["text"] = f"{prev['text']} {sentence_text}".strip()
            else:
                sentences.append({
                    "start": round(current_start, 3),
                    "end": round(current_end, 3),
                    "text": sentence_text
                })

            current_words = []
            current_start = None

    if current_words and current_start is not None:
        sentence_text = " ".join(current_words)
        if sentences:
            prev = sentences[-1]
            prev["end"] = round(all_words[-1].end, 3)
            prev["text"] = f"{prev['text']} {sentence_text}".strip()
        else:
            sentences.append({
                "start": round(current_start, 3),
                "end": round(all_words[-1].end, 3),
                "text": sentence_text
            })

    sys.stdout.reconfigure(encoding='utf-8')
    json_output = json.dumps(sentences, ensure_ascii=False)
    print(json_output)
    log(f"Thành công! Đã bóc được {len(sentences)} mốc câu.")

if __name__ == "__main__":
    main()
