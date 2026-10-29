# Hướng dẫn: Runner ARM64 trên GitHub Actions

Tài liệu giải thích runner ARM64 là gì, vì sao VibeZone cần nó để deploy lên EC2 t4g (ADR-0014), cách
`.github/workflows/ci.yml` dùng nó, và phải làm gì khi không dùng được.

---

## 1. Runner là gì?

Khi bạn push code, GitHub Actions cần **một máy** để chạy các bước trong workflow (build, test, đóng gói
image…). Máy đó gọi là **runner**.

- **GitHub-hosted runner**: máy ảo do GitHub cấp, tạo mới cho mỗi job rồi xoá đi. Bạn chọn loại máy bằng
  dòng `runs-on:` trong workflow, ví dụ `runs-on: ubuntu-latest`.
- **Self-hosted runner**: máy của chính bạn (PC, VPS, EC2) cài phần mềm runner của GitHub để nhận job.

Mỗi job trong workflow chạy trên một runner riêng.

## 2. ARM64 là gì? Khác gì x86-64?

CPU có nhiều **kiến trúc tập lệnh**. Hai loại phổ biến nhất:

| Kiến trúc | Tên khác | Gặp ở đâu |
|---|---|---|
| **x86-64** | amd64, x64 | Hầu hết PC/laptop Intel, AMD; EC2 `t3`, `m5`, `c5`…; runner `ubuntu-latest` |
| **ARM64** | aarch64, arm64 | Apple M1/M2/M3, điện thoại, Raspberry Pi; EC2 **Graviton** (`t4g`, `m7g`, `c7g`…) |

Một chương trình đã biên dịch cho kiến trúc này **không chạy trực tiếp** trên kiến trúc kia. Docker image
cũng vậy: image build cho `linux/amd64` chạy trên máy ARM64 sẽ lỗi:

```
exec /usr/bin/dotnet: exec format error
```

## 3. Vì sao VibeZone cần ARM64?

Theo ADR-0014, máy chủ là **EC2 t4g.medium** (chip AWS Graviton = ARM64). Lý do chọn t4g: rẻ hơn khoảng
20 % so với `t3.medium` (x86) cùng cấu hình, rất hợp với ngân sách 100 USD.

Vì máy chủ là ARM64 nên **image phải build cho `linux/arm64`**. Có 3 cách:

| Cách | Tốc độ | Chi phí | Ghi chú |
|---|---|---|---|
| **A. Runner ARM64 của GitHub** (`ubuntu-24.04-arm`) | Nhanh (chạy thật trên CPU ARM) | Miễn phí cho repo **public** | Cách đang dùng |
| B. Giả lập bằng QEMU trên runner x86 | Chậm 5–10 lần, .NET/Node đôi khi lỗi khi giả lập | Miễn phí (tính phút Actions với repo private) | Dự phòng khi repo private |
| C. Đổi máy chủ sang x86 (`t3.medium`) và build `linux/amd64` | Nhanh | Máy chủ đắt hơn ~20 % | Không cần runner ARM |

## 4. Runner ARM64 của GitHub (cách A)

GitHub cung cấp sẵn máy ảo Linux chạy CPU ARM64. Trong workflow chỉ cần:

```yaml
runs-on: ubuntu-24.04-arm
```

Điều kiện quan trọng:
- **Miễn phí cho repository public** — đây là lý do bạn nên mở public repo.
- Với repository **private**, tuỳ gói GitHub mà nhãn này có thể không có hoặc bị tính phí. Chính sách thay đổi
  theo thời gian — kiểm tra trang *About GitHub-hosted runners* trong tài liệu GitHub trước khi dùng.
  Nếu không được cấp, job sẽ đứng mãi ở trạng thái **"Waiting for a runner to pick up this job…"**.

## 5. VibeZone dùng nó thế nào?

Trong `.github/workflows/ci.yml`, job `publish` (chỉ chạy khi push lên `main`, sau khi mọi test kể cả e2e
đã xanh):

```yaml
publish:
  runs-on: ${{ vars.ARM_RUNNER || 'ubuntu-24.04-arm' }}
  ...
  - uses: docker/build-push-action@v7
    with:
      platforms: linux/arm64
      push: true
      tags: ghcr.io/<owner>/vibezone-api:<commit-sha>
```

- `vars.ARM_RUNNER || 'ubuntu-24.04-arm'`: mặc định dùng runner ARM64 của GitHub. Nếu bạn đặt biến repo
  `ARM_RUNNER` thì workflow dùng giá trị đó (ví dụ nhãn của self-hosted runner).
- Job chạy song song 4 lần (matrix) cho 4 image: `api`, `migrator`, `worker`, `web`.
- Các job khác (build, test, e2e, kiểm tra build image ở PR) vẫn chạy trên `ubuntu-latest` (x86). Chỉ bước
  đóng gói image để deploy mới cần ARM64.

Đổi `ARM_RUNNER`: GitHub repo → **Settings → Secrets and variables → Actions → Variables → New repository
variable** → Name `ARM_RUNNER`, Value ví dụ `self-hosted`. Xoá biến này để quay về runner của GitHub.

## 6. Kiểm tra image đúng kiến trúc

Sau khi job `publish` xanh:

```bash
docker buildx imagetools inspect ghcr.io/ndgiang02/vibezone-api:<commit-sha>
# Platform: linux/arm64   ← phải thấy dòng này
```

Trên máy chủ:

```bash
uname -m          # aarch64 = ARM64
docker run --rm ghcr.io/ndgiang02/vibezone-api:<commit-sha> --version   # không có "exec format error"
```

## 7. Nếu repo vẫn để private (cách B — QEMU)

Sửa job `publish` trong `ci.yml`:

```yaml
publish:
  runs-on: ubuntu-latest                 # x86
  steps:
    - uses: actions/checkout@v7
    - uses: docker/setup-qemu-action@v3  # thêm: giả lập ARM64
    - uses: docker/setup-buildx-action@v4
    ...
    - uses: docker/build-push-action@v7
      with:
        platforms: linux/arm64           # giữ nguyên
```

Lưu ý: build .NET và Next.js dưới QEMU có thể mất 20–40 phút mỗi image và thỉnh thoảng lỗi ngẫu nhiên; tăng
`timeout-minutes` của job lên ~90. Repo private còn bị trừ phút Actions miễn phí của tài khoản.

## 8. Nếu đổi máy chủ sang x86 (cách C)

1. Tạo EC2 `t3.medium` (Ubuntu 24.04 **x86_64**) thay vì `t4g.medium`; ở runbook, bản AWS CLI dùng
   `awscli-exe-linux-x86_64.zip`.
2. Trong `ci.yml`, job `publish`: `runs-on: ubuntu-latest` và `platforms: linux/amd64`.
3. Cập nhật ADR-0014 (kiến trúc máy chủ và chi phí).

## 9. Self-hosted runner (tuỳ chọn)

Có thể dùng chính một máy ARM64 (ví dụ Mac M1, hoặc một EC2 t4g khác) làm runner:
GitHub repo → **Settings → Actions → Runners → New self-hosted runner** → chọn Linux/macOS + ARM64 → làm theo
lệnh hiển thị, rồi đặt `ARM_RUNNER=self-hosted`.

**Không khuyến nghị cho repo public**: bất kỳ ai mở pull request đều có thể khiến code của họ chạy trên máy
của bạn. Đừng bao giờ dùng máy chủ production làm runner.

## 10. Xử lý sự cố

| Triệu chứng | Nguyên nhân | Cách xử lý |
|---|---|---|
| Job `publish` đứng ở "Waiting for a runner…" | Repo private không được cấp `ubuntu-24.04-arm` | Mở public repo, hoặc dùng cách B / C |
| Container trên máy chủ báo `exec format error` | Image sai kiến trúc so với máy chủ | Kiểm tra theo §6; `platforms` phải khớp máy chủ |
| Build dưới QEMU rất chậm hoặc lỗi `Segmentation fault` | Giả lập không ổn định với .NET/Node | Chạy lại job; tốt nhất dùng runner ARM64 thật |
| `docker pull` trên máy chủ báo `no matching manifest for linux/arm64` | Image chỉ có bản amd64 | Build lại với `platforms: linux/arm64` |
| `publish` không chạy | Chỉ chạy khi push lên `main` và CI (kể cả e2e) đã xanh | Xem các job trước bị đỏ ở đâu |

Xem thêm: [deploy-aws.md](deploy-aws.md) (toàn bộ quy trình triển khai), [ADR-0014](../adr/0014-single-host-aws-deployment.md).
