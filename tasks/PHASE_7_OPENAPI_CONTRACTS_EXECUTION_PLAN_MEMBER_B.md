# Phase 7 — OpenAPI và API contracts (Member B)

## Trạng thái rà soát: Chưa nghiệm thu

### Đã có

- `GET /api/catalog` và danh sách metadata các service.
- Bộ test tolerant-reader/serialization cho event contracts.
- Bộ test khung cho catalog, endpoint conventions và ProblemDetails.

### Chưa đạt hoặc chưa có bằng chứng

- Catalog trỏ tới `/identity/openapi.json`, `/scheduling/openapi.json`, `/appointment/openapi.json`, `/queue/openapi.json`, `/medical-records/openapi.json`, `/notification/openapi.json`; hiện chưa có route proxy/generate tương ứng. Gateway hiện công bố spec của chính Gateway tại `/openapi/v1.json`.
- `ApiContractDriftTests` chưa tải OpenAPI document hay đối chiếu route/DTO/auth thật. Một số assertion chỉ xác nhận chuỗi/mẫu cố định nên không phát hiện drift.
- Chưa có kiểm tra tự động mọi HTTP API trong phạm vi đều có service-owned OpenAPI, cũng chưa lưu snapshot/baseline đã validate.
- Event compatibility test chỉ kiểm chứng serializer/type được chọn; chưa chứng minh tất cả consumer hiện hành tương thích với schema evolution.

### Cách khắc phục

1. Lập inventory route/DTO/auth/error cho từng service và xác định owner.
2. Sinh/serve OpenAPI từ từng service; thêm Gateway link/aggregation chỉ tới URL đã chạy được. Nếu spec chưa tồn tại, catalog phải thể hiện rõ trạng thái unavailable thay vì URL giả.
3. Viết integration test khởi chạy app thật, tải từng spec, kiểm tra route inventory, auth, response/error schema và lưu snapshot để phát hiện breaking changes.
4. Với event contracts, kiểm tra payload cũ/mới qua consumer serializer/deserializer thực tế; ghi rõ quy tắc versioning.
5. Chỉ đánh dấu phase hoàn tất sau khi toàn bộ spec/test chạy trong CI hoặc lệnh nghiệm thu tái lập được.

### Tiêu chí đóng

- [ ] Mọi API trong scope có OpenAPI hợp lệ và service owner rõ.
- [ ] Catalog chỉ công bố URL có thể truy cập.
- [ ] Drift test phát hiện được route/schema/auth thay đổi có chủ ý trong fixture.
- [ ] Event compatibility test bao phủ contract và consumer liên quan.
- [ ] Có lệnh chạy và kết quả kiểm chứng trên app thật.

Các số đếm test ghi trong báo cáo cũ là kết quả test đã chạy tại thời điểm đó, nhưng không chứng minh các tiêu chí trên; không dùng chúng làm bằng chứng nghiệm thu Phase 7.
