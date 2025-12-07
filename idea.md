# PROJECT SPECIFICATION: DEVSTICKY (QUICK NOTE STICKY)

## 1. Tổng quan dự án (Project Overview)
* **Tên dự án:** DevSticky
* **Slogan:** "The Invisible Scratchpad for Developers" (Giấy nháp tàng hình cho lập trình viên).
* **Mô tả:** Một ứng dụng ghi chú siêu nhẹ, luôn nổi trên màn hình (Always on Top), hỗ trợ độ trong suốt và tối ưu hóa đặc biệt cho việc lưu trữ các đoạn mã (Code Snippet), JSON, Log lỗi tạm thời.
* **Mục tiêu:** Thay thế Sticky Notes của Windows bằng một phiên bản "Developer-centric": nhẹ hơn, thông minh hơn và đẹp hơn.

---

## 2. Công nghệ sử dụng (Tech Stack)
Để đạt được tiêu chí **"Tối ưu hiệu năng & Tốn ít RAM nhất"**, dự án sẽ sử dụng stack sau:

| Thành phần | Công nghệ lựa chọn | Lý do (Justification) |
| :--- | :--- | :--- |
| **Ngôn ngữ** | **C# (.NET 8)** | Tận dụng hệ sinh thái mạnh mẽ, cú pháp hiện đại. |
| **Framework** | **WPF (Windows Presentation Foundation)** | Hỗ trợ tùy biến UI cực mạnh (Transparent, Layering, Border Radius) dễ dàng hơn WinForms. |
| **Biên dịch** | **Native AOT** (Ahead-of-Time) | **Critical:** Biên dịch thẳng ra mã máy, bỏ qua JIT Compiler giúp khởi động tức thì và giảm RAM xuống mức tối thiểu (~10-15MB). |
| **Kiến trúc** | **MVVM** (Model-View-ViewModel) | Tách biệt giao diện (View) và logic (ViewModel), code sạch, dễ bảo trì. |
| **Editor Engine** | **AvalonEdit** | Thư viện soạn thảo code mã nguồn mở siêu nhẹ (dùng trong SharpDevelop), hỗ trợ tô màu cú pháp (Syntax Highlighting). |
| **Lưu trữ** | **JSON File** | Đơn giản, nhanh, dễ backup, không cần Database Engine cồng kềnh. |

---

## 3. Chức năng chi tiết (Functional Requirements)

### A. Core Features (Cốt lõi - Phải có)
1.  **Always on Top (Ghim cửa sổ):**
    * Cửa sổ Note luôn nằm trên các ứng dụng khác (IDE, Browser).
    * Có nút Toggle (Ghim/Bỏ ghim) trên thanh tiêu đề.
2.  **Windowless & Resizable (Giao diện không viền):**
    * Loại bỏ thanh tiêu đề mặc định của Windows.
    * Người dùng có thể kéo thả bất kỳ đâu trên Note để di chuyển.
    * Có Grip ở góc dưới phải để thay đổi kích thước.
3.  **Opacity Control (Độ trong suốt):**
    * Thanh trượt (Slider) hoặc phím tắt để chỉnh độ mờ (20% - 100%).
    * **Use case:** Đặt Note đè lên code mẫu để gõ lại mà vẫn nhìn thấy nội dung bên dưới.
4.  **Auto-Save & Auto-Restore:**
    * Lưu nội dung ngay lập tức khi người dùng ngừng gõ (debounce 500ms).
    * Tự động khôi phục vị trí, kích thước và nội dung của tất cả các Note khi mở lại ứng dụng.

### B. Developer Features (Tính năng nâng cao)
5.  **Syntax Highlighting (Tô màu mã):**
    * Tự động nhận diện hoặc chọn thủ công: C#, Java, JS, JSON, SQL, XML.
6.  **Quick Format (Format nhanh):**
    * Paste JSON/XML một dòng -> Bấm phím tắt (VD: Ctrl+Shift+F) -> Tự động Pretty Print (xuống dòng, thụt lề).
7.  **Multi-Instance (Đa cửa sổ):**
    * Cho phép tạo nhiều Note cùng lúc.
    * Quản lý danh sách Note qua System Tray (icon ở góc đồng hồ).

---

## 4. Thiết kế giao diện (UI/UX Design)

* **Theme:** Dark Mode (Mặc định) - Dùng màu dịu mắt (VD: Dracula Theme hoặc One Dark Pro).
* **Font chữ:** Consolas hoặc JetBrains Mono (Font Monospace để hiển thị code chuẩn).
* **Layout:**
    * **Header (Ẩn/Hiện):** Chỉ hiện khi di chuột vào vùng trên cùng (cao 20px). Chứa nút: `+` (New), `⚙` (Setting), `📌` (Pin), `✖` (Close).
    * **Content:** Chiếm toàn bộ diện tích còn lại. Không có viền (Border = 0).
    * **Background:** Màu tối bán trong suốt (Semi-transparent dark color).

---

## 5. Cấu trúc dữ liệu (Data Structure)

Dữ liệu sẽ được lưu tại: `%AppData%\DevSticky\notes.json`

**Schema file JSON:**
```json
{
  "appSettings": {
    "defaultOpacity": 0.9,
    "theme": "Dark",
    "startWithWindows": true
  },
  "notes": [
    {
      "id": "guid-uuid-v4",
      "content": "docker run -d -p 80:80 nginx",
      "language": "bash",
      "isPinned": true,
      "opacity": 0.8,
      "windowRect": {
        "top": 100,
        "left": 500,
        "width": 300,
        "height": 200
      },
      "createdDate": "2025-12-06T10:00:00Z"
    }
  ]
}
````

-----

## 6\. Lộ trình phát triển (Development Roadmap)

### Phase 1: The Skeleton (Khung sườn)

  * [ ] Tạo Project WPF .NET 8.
  * [ ] Thiết kế cửa sổ Windowless, cho phép kéo thả (DragMove).
  * [ ] Làm nút Close, Resize thủ công.

### Phase 2: The Core (Lõi chức năng)

  * [ ] Tích hợp AvalonEdit làm vùng soạn thảo.
  * [ ] Xử lý Logic Auto-save xuống file JSON.
  * [ ] Xử lý Logic Load dữ liệu cũ lên khi khởi động.

### Phase 3: The Polish (Đánh bóng)

  * [ ] Thêm Slider chỉnh độ trong suốt (Opacity).
  * [ ] Thêm nút Pin (Always on Top).
  * [ ] Cấu hình Native AOT để tối ưu dung lượng và RAM.

### Phase 4: Advanced (Nâng cao)

  * [ ] Chức năng Format JSON.
  * [ ] System Tray Icon (Chạy ngầm).

<!-- end list -->

```