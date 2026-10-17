/**
 * ECO-ARECA Products Catalog Data
 * Compliant with specifications in Block 3:
 * 5 Product Groups with dimensions, usage descriptions, variants, and demo retail pricing.
 */

const ECO_PRODUCTS = [
  {
    id: "chen-vuong-cham",
    name: "Chén Vuông Chấm Gia Vị",
    category: "chen",
    dimensions: "7.5 × 7.5 × 2 cm",
    shortDesc: "Chuyên dụng đựng các loại nước chấm, muối tiêu chanh, gia vị, mù tạt và đồ ăn kèm khai vị.",
    fullDesc: "Sản phẩm được ép nhiệt định hình từ bẹ mo cau già tự nhiên, thành chén dày dặn, không bị thấm nước chấm cay nồng, dầu mè hay giấm ớt. Thích hợp cho mâm cơm gia đình, tiệc BBQ ngoài trời và dịch vụ nhà hàng sinh thái.",
    image: "assets/images/chen-vuong-cham.jpg",
    badge: "Bán Chạy",
    badgeClass: "hot",
    isBestSeller: true,
    variants: [
      { label: "Set 10 chiếc", price: 18000, pricePerUnit: 1800, default: true },
      { label: "Set 25 chiếc", price: 42000, pricePerUnit: 1680 },
      { label: "Set 50 chiếc", price: 80000, pricePerUnit: 1600 }
    ]
  },
  {
    id: "chen-tron-sup",
    name: "Chén Tròn Súp / Chè",
    category: "chen",
    dimensions: "Đường kính 12 cm — Sâu 4 cm",
    shortDesc: "Thích hợp cho súp khai vị, cháo, canh nóng, chè sen, sữa hạt và các món tráng miệng ngọt.",
    fullDesc: "Độ sâu 4cm hoàn hảo chứa súp nóng mà không làm mềm vỏ hay tỏa mùi khó chịu. Lớp sáp tự nhiên trên bề mặt bẹ mo cau giúp chống thấm nước sôi và dầu mỡ tự nhiên mà không cần bất kỳ lớp tráng nhựa vi sinh nào.",
    image: "assets/images/chen-tron-sup.jpg",
    badge: "Ưa Chuộng",
    badgeClass: "eco",
    isBestSeller: false,
    variants: [
      { label: "Set 10 chiếc", price: 30000, pricePerUnit: 3000, default: true },
      { label: "Set 25 chiếc", price: 70000, pricePerUnit: 2800 },
      { label: "Set 50 chiếc", price: 135000, pricePerUnit: 2700 }
    ]
  },
  {
    id: "dia-tron-mo-cau",
    name: "Dĩa Tròn Mo Cau Tự Nhiên",
    category: "dia-tron",
    dimensions: "15 cm, 20 cm, 25 cm",
    shortDesc: "Dĩa tròn đa năng phục vụ món ăn chính, món xào, gỏi cuốn, bánh ngọt và đồ nướng sang trọng.",
    fullDesc: "Sở hữu những đường vân mo cau mộc mạc độc bản, từng chiếc dĩa tròn ECO-ARECA làm nổi bật màu sắc tươi ngon của thực phẩm. Chịu lực tốt khi cầm tay tại tiệc đứng (cocktail / buffet ngoài trời).",
    image: "assets/images/dia-tron-mo-cau.jpg",
    badge: "Đặc Trưng",
    badgeClass: "eco",
    isBestSeller: true,
    variants: [
      { label: "Dĩa 15 cm (Set 10)", price: 35000, pricePerUnit: 3500, default: false },
      { label: "Dĩa 20 cm (Set 10)", price: 45000, pricePerUnit: 4500, default: true },
      { label: "Dĩa 25 cm (Set 10)", price: 55000, pricePerUnit: 5500, default: false }
    ]
  },
  {
    id: "dia-vuong-bbq",
    name: "Dĩa Vuông / Khay BBQ Mo Cau",
    category: "dia-vuong",
    dimensions: "15 × 15 cm, 20 × 20 cm, 18 × 24 cm",
    shortDesc: "Tuyệt phẩm cho tiệc nướng BBQ, burger, sandwich, teabreak sự kiện và finger food.",
    fullDesc: "Thiết kế thành viền vuông vắn cứng cáp, chịu được độ nóng từ xiên nướng than hoa và không bị cong vênh khi để thịt nướng ngập sốt BBQ đậm đà. Đem lại trải nghiệm dã ngoại vừa hiện đại vừa thân thiện.",
    image: "assets/images/dia-vuong-bbq.jpg",
    badge: "Tiệc BBQ",
    badgeClass: "hot",
    isBestSeller: true,
    variants: [
      { label: "15 × 15 cm (Set 10)", price: 39000, pricePerUnit: 3900, default: false },
      { label: "20 × 20 cm (Set 10)", price: 52000, pricePerUnit: 5200, default: true },
      { label: "18 × 24 cm (Set 10)", price: 65000, pricePerUnit: 6500, default: false }
    ]
  },
  {
    id: "combo-gia-dinh",
    name: "Combo Gia Đình Tiện Lợi",
    category: "combo",
    dimensions: "Bộ tổng hợp đa kích thước",
    shortDesc: "Đầy đủ dĩa chính, dĩa phụ, chén súp, chén chấm và thìa nĩa gỗ, đóng trong hộp kraft sang trọng.",
    fullDesc: "Giải pháp trọn gói cho chuyến du lịch picnic, cắm trại cuối tuần của cả gia đình 4-6 người hoặc tiệc họp mặt bạn bè. Hộp quà sinh thái trang nhã, sẵn sàng sử dụng ngay mà không cần chuẩn bị rửa dọn.",
    image: "assets/images/combo-gia-dinh.jpg",
    badge: "Gợi Ý Khuyên Dùng",
    badgeClass: "hot",
    isBestSeller: true,
    variants: [
      { label: "Set 10 món tổng hợp", price: 65000, pricePerUnit: 6500, default: false },
      { label: "Set 25 món gia đình", price: 145000, pricePerUnit: 5800, default: true }
    ]
  },
  {
    id: "quat-mo-cau",
    name: "Quạt Mo Cau Tròn Liền Cán",
    category: "quat",
    dimensions: "22 × 36 cm (Nguyên khối liền cán)",
    shortDesc: "Quạt mo cau ép nhiệt nguyên khối liền cán, bề mặt vân mo tự nhiên mộc mạc, nhẹ tay và gió mát đượm.",
    fullDesc: "Được ép nhiệt định hình nguyên khối từ bẹ mo cau già tự nhiên không chắp ghép, cán cầm liền thân đầm tay và chắc chắn. Bề mặt nhẵn bóng tự nhiên với các đường vân bẹ cau sọc dọc độc bản. Thích hợp dùng trong gia đình, các khu du lịch sinh thái, tiệc ngoài trời và quà tặng truyền thống.",
    image: "assets/images/quat-mo-cau.png",
    badge: "Mới Ra Mắt",
    badgeClass: "hot",
    isBestSeller: true,
    variants: [
      { label: "1 Chiếc lẻ", price: 10000, pricePerUnit: 10000, default: true },
      { label: "Set 10 chiếc (9k/c)", price: 90000, pricePerUnit: 9000, default: false },
      { label: "Set 20 chiếc (8k/c)", price: 160000, pricePerUnit: 8000, default: false },
      { label: "Set 50 chiếc (7k/c)", price: 350000, pricePerUnit: 7000, default: false }
    ]
  },
  {
    id: "quat-khac-laser-hue",
    name: "Quạt Mo Cau Khắc Laser Huế / Theo Yêu Cầu",
    category: "quat",
    dimensions: "22 × 36 cm (Khắc laser tinh xảo)",
    shortDesc: "Quạt mo cau khắc laser thắng cảnh Cố Đô Huế (Ngọ Môn, Chùa Thiên Mụ, Cầu Trường Tiền) hoặc khắc logo, chữ theo yêu cầu.",
    fullDesc: "Sản phẩm quà tặng du lịch & lưu niệm độc đáo mang đậm bản sắc văn hóa Cố Đô. Bề mặt quạt mo cau tự nhiên được khắc laser công nghệ cao sắc nét với các danh lam thắng cảnh biểu tượng của Huế hoặc nhận khắc logo doanh nghiệp, tên cặp đôi, câu chúc, slogan sự kiện theo yêu cầu. Giá lẻ cực tốt chỉ 15.000đ/cái, chiết khấu sâu khi mua số lượng nhiều.",
    image: "assets/images/quat-khac-laser-hue.jpg",
    badge: "Đặc Sản Huế",
    badgeClass: "hot",
    isBestSeller: true,
    variants: [
      { label: "1 Chiếc lẻ", price: 15000, pricePerUnit: 15000, default: true },
      { label: "Set 10 chiếc (13.5k/c)", price: 135000, pricePerUnit: 13500, default: false },
      { label: "Set 20 chiếc (12k/c)", price: 240000, pricePerUnit: 12000, default: false },
      { label: "Set 50 chiếc (11k/c)", price: 550000, pricePerUnit: 11000, default: false }
    ]
  }
];

// Helper to format VND Currency
function formatCurrency(amount) {
  return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })
    .format(amount)
    .replace('₫', 'đ');
}
