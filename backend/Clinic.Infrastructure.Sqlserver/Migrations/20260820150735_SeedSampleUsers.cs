using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Clinic.Infrastructure.Sqlserver.Migrations
{
    /// <inheritdoc />
    public partial class SeedSampleUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Persons",
                columns: new[] { "Id", "Address", "CreatedAt", "DateOfBirth", "Email", "FullName", "Gender", "IsDeleted", "PhoneNumber", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("a0000001-0000-0000-0000-000000000001"), "1201 Nguyen Trai, District 2, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1978, 4, 12), "doctor1@clinic.local", "Nguyen Van An", 0, false, "0901000001", null },
                    { new Guid("a0000001-0000-0000-0000-000000000002"), "1202 Nguyen Trai, District 3, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1982, 8, 3), "doctor2@clinic.local", "Tran Thi Binh", 1, false, "0901000002", null },
                    { new Guid("a0000001-0000-0000-0000-000000000003"), "1203 Nguyen Trai, District 4, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1975, 1, 21), "doctor3@clinic.local", "Le Minh Chau", 0, false, "0901000003", null },
                    { new Guid("a0000001-0000-0000-0000-000000000004"), "1204 Nguyen Trai, District 5, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1980, 11, 9), "doctor4@clinic.local", "Pham Quoc Dung", 0, false, "0901000004", null },
                    { new Guid("a0000001-0000-0000-0000-000000000005"), "1205 Nguyen Trai, District 6, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1985, 2, 17), "doctor5@clinic.local", "Hoang Thi Em", 1, false, "0901000005", null },
                    { new Guid("a0000001-0000-0000-0000-000000000006"), "1206 Nguyen Trai, District 7, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1979, 7, 28), "doctor6@clinic.local", "Vu Van Phuc", 0, false, "0901000006", null },
                    { new Guid("a0000001-0000-0000-0000-000000000007"), "1207 Nguyen Trai, District 8, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1983, 5, 6), "doctor7@clinic.local", "Dang Thi Giang", 1, false, "0901000007", null },
                    { new Guid("a0000001-0000-0000-0000-000000000008"), "1208 Nguyen Trai, District 9, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1976, 12, 14), "doctor8@clinic.local", "Bui Van Hai", 0, false, "0901000008", null },
                    { new Guid("a0000001-0000-0000-0000-000000000009"), "1209 Nguyen Trai, District 10, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1981, 9, 30), "doctor9@clinic.local", "Ngo Thi Hoa", 1, false, "0901000009", null },
                    { new Guid("a0000001-0000-0000-0000-000000000010"), "1210 Nguyen Trai, District 11, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1974, 3, 18), "doctor10@clinic.local", "Duong Van Khoa", 0, false, "0901000010", null },
                    { new Guid("a0000001-0000-0000-0000-000000000011"), "1211 Nguyen Trai, District 12, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1984, 6, 22), "doctor11@clinic.local", "Luong Thi Lan", 1, false, "0901000011", null },
                    { new Guid("a0000001-0000-0000-0000-000000000012"), "1212 Nguyen Trai, District 1, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1977, 10, 5), "doctor12@clinic.local", "Trinh Van Minh", 0, false, "0901000012", null },
                    { new Guid("a0000001-0000-0000-0000-000000000013"), "1213 Nguyen Trai, District 2, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1986, 1, 11), "doctor13@clinic.local", "Cao Thi Nga", 1, false, "0901000013", null },
                    { new Guid("a0000001-0000-0000-0000-000000000014"), "1214 Nguyen Trai, District 3, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1973, 8, 19), "doctor14@clinic.local", "Phan Van Quang", 0, false, "0901000014", null },
                    { new Guid("a0000001-0000-0000-0000-000000000015"), "1215 Nguyen Trai, District 4, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1988, 4, 25), "doctor15@clinic.local", "Do Thi Quyen", 1, false, "0901000015", null },
                    { new Guid("a0000001-0000-0000-0000-000000000016"), "1216 Nguyen Trai, District 5, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1972, 2, 8), "admin1@clinic.local", "Vo Thanh Son", 0, false, "0901000016", null },
                    { new Guid("a0000001-0000-0000-0000-000000000017"), "1217 Nguyen Trai, District 6, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1980, 12, 1), "admin2@clinic.local", "Mai Huu Tam", 0, false, "0901000017", null },
                    { new Guid("a0000001-0000-0000-0000-000000000018"), "1218 Nguyen Trai, District 7, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1992, 3, 14), "receptionist1@clinic.local", "Ly Thi Uyen", 1, false, "0901000018", null },
                    { new Guid("a0000001-0000-0000-0000-000000000019"), "1219 Nguyen Trai, District 8, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1990, 7, 7), "receptionist2@clinic.local", "Ho Van Vinh", 0, false, "0901000019", null },
                    { new Guid("a0000001-0000-0000-0000-000000000020"), "1220 Nguyen Trai, District 9, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1993, 11, 23), "receptionist3@clinic.local", "Dinh Thi Xuan", 1, false, "0901000020", null },
                    { new Guid("a0000001-0000-0000-0000-000000000021"), "1221 Nguyen Trai, District 10, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1989, 5, 16), "receptionist4@clinic.local", "Ta Van Yen", 0, false, "0901000021", null },
                    { new Guid("a0000001-0000-0000-0000-000000000022"), "1222 Nguyen Trai, District 11, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1991, 9, 2), "receptionist5@clinic.local", "Chau Thi Anh", 1, false, "0901000022", null },
                    { new Guid("a0000001-0000-0000-0000-000000000023"), "1223 Nguyen Trai, District 12, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1995, 1, 4), "patient1@clinic.local", "Nguyen Thi Bach", 1, false, "0901000023", null },
                    { new Guid("a0000001-0000-0000-0000-000000000024"), "1224 Nguyen Trai, District 1, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1987, 6, 18), "patient2@clinic.local", "Tran Van Cuong", 0, false, "0901000024", null },
                    { new Guid("a0000001-0000-0000-0000-000000000025"), "1225 Nguyen Trai, District 2, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1998, 8, 9), "patient3@clinic.local", "Le Thi Dao", 1, false, "0901000025", null },
                    { new Guid("a0000001-0000-0000-0000-000000000026"), "1226 Nguyen Trai, District 3, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1994, 2, 27), "patient4@clinic.local", "Pham Van Dat", 0, false, "0901000026", null },
                    { new Guid("a0000001-0000-0000-0000-000000000027"), "1227 Nguyen Trai, District 4, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2000, 10, 13), "patient5@clinic.local", "Hoang Thi Hanh", 1, false, "0901000027", null },
                    { new Guid("a0000001-0000-0000-0000-000000000028"), "1228 Nguyen Trai, District 5, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1986, 12, 20), "patient6@clinic.local", "Huynh Van Hung", 0, false, "0901000028", null },
                    { new Guid("a0000001-0000-0000-0000-000000000029"), "1229 Nguyen Trai, District 6, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1996, 4, 8), "patient7@clinic.local", "Phan Thi Kim", 1, false, "0901000029", null },
                    { new Guid("a0000001-0000-0000-0000-000000000030"), "1230 Nguyen Trai, District 7, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1991, 7, 31), "patient8@clinic.local", "Vu Van Long", 0, false, "0901000030", null },
                    { new Guid("a0000001-0000-0000-0000-000000000031"), "1231 Nguyen Trai, District 8, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1999, 3, 3), "patient9@clinic.local", "Vo Thi Mai", 1, false, "0901000031", null },
                    { new Guid("a0000001-0000-0000-0000-000000000032"), "1232 Nguyen Trai, District 9, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1985, 5, 15), "patient10@clinic.local", "Dang Van Nam", 0, false, "0901000032", null },
                    { new Guid("a0000001-0000-0000-0000-000000000033"), "1233 Nguyen Trai, District 10, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1997, 9, 21), "patient11@clinic.local", "Bui Thi Oanh", 1, false, "0901000033", null },
                    { new Guid("a0000001-0000-0000-0000-000000000034"), "1234 Nguyen Trai, District 11, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1993, 1, 29), "patient12@clinic.local", "Ngo Van Phat", 0, false, "0901000034", null },
                    { new Guid("a0000001-0000-0000-0000-000000000035"), "1235 Nguyen Trai, District 12, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2001, 11, 6), "patient13@clinic.local", "Duong Thi Quynh", 1, false, "0901000035", null },
                    { new Guid("a0000001-0000-0000-0000-000000000036"), "1236 Nguyen Trai, District 1, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1984, 8, 12), "patient14@clinic.local", "Luong Van Sang", 0, false, "0901000036", null },
                    { new Guid("a0000001-0000-0000-0000-000000000037"), "1237 Nguyen Trai, District 2, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1992, 2, 2), "patient15@clinic.local", "Trinh Thi Trang", 1, false, "0901000037", null },
                    { new Guid("a0000001-0000-0000-0000-000000000038"), "1238 Nguyen Trai, District 3, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1988, 6, 26), "patient16@clinic.local", "Cao Van Tuan", 0, false, "0901000038", null },
                    { new Guid("a0000001-0000-0000-0000-000000000039"), "1239 Nguyen Trai, District 4, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1996, 12, 17), "patient17@clinic.local", "Phan Thi Uyen", 1, false, "0901000039", null },
                    { new Guid("a0000001-0000-0000-0000-000000000040"), "1240 Nguyen Trai, District 5, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1983, 4, 4), "patient18@clinic.local", "Do Van Viet", 0, false, "0901000040", null },
                    { new Guid("a0000001-0000-0000-0000-000000000041"), "1241 Nguyen Trai, District 6, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2002, 7, 19), "patient19@clinic.local", "Ly Thi Xinh", 1, false, "0901000041", null },
                    { new Guid("a0000001-0000-0000-0000-000000000042"), "1242 Nguyen Trai, District 7, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1990, 10, 10), "patient20@clinic.local", "Ho Van Yen", 0, false, "0901000042", null },
                    { new Guid("a0000001-0000-0000-0000-000000000043"), "1243 Nguyen Trai, District 8, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1994, 1, 22), "patient21@clinic.local", "Dinh Thi An", 1, false, "0901000043", null },
                    { new Guid("a0000001-0000-0000-0000-000000000044"), "1244 Nguyen Trai, District 9, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1989, 3, 27), "patient22@clinic.local", "Ta Van Bao", 0, false, "0901000044", null },
                    { new Guid("a0000001-0000-0000-0000-000000000045"), "1245 Nguyen Trai, District 10, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1998, 5, 5), "patient23@clinic.local", "Chau Thi Cam", 1, false, "0901000045", null },
                    { new Guid("a0000001-0000-0000-0000-000000000046"), "1246 Nguyen Trai, District 11, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1979, 9, 14), "patient24@clinic.local", "Nguyen Van Duc", 0, false, "0901000046", null },
                    { new Guid("a0000001-0000-0000-0000-000000000047"), "1247 Nguyen Trai, District 12, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2003, 8, 8), "patient25@clinic.local", "Tran Thi En", 1, false, "0901000047", null },
                    { new Guid("a0000001-0000-0000-0000-000000000048"), "1248 Nguyen Trai, District 1, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1995, 11, 11), "patient26@clinic.local", "Le Van Giang", 0, false, "0901000048", null },
                    { new Guid("a0000001-0000-0000-0000-000000000049"), "1249 Nguyen Trai, District 2, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1982, 2, 14), "patient27@clinic.local", "Pham Thi Hien", 1, false, "0901000049", null },
                    { new Guid("a0000001-0000-0000-0000-000000000050"), "1250 Nguyen Trai, District 3, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1997, 6, 1), "patient28@clinic.local", "Hoang Van Kiet", 0, false, "0901000050", null },
                    { new Guid("a0000001-0000-0000-0000-000000000051"), "1251 Nguyen Trai, District 4, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2000, 12, 24), "patient29@clinic.local", "Huynh Thi Linh", 1, false, "0901000051", null },
                    { new Guid("a0000001-0000-0000-0000-000000000052"), "1252 Nguyen Trai, District 5, Ho Chi Minh City", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(1981, 4, 30), "patient30@clinic.local", "Phan Van My", 0, false, "0901000052", null }
                });

            migrationBuilder.InsertData(
                table: "Specialties",
                columns: new[] { "Id", "CreatedAt", "Description", "EstablishedDate", "IsDeleted", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("a0000006-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Heart and cardiovascular care", new DateOnly(2010, 1, 15), false, "Cardiology", null },
                    { new Guid("a0000006-0000-0000-0000-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Skin, hair, and nail care", new DateOnly(2012, 3, 1), false, "Dermatology", null },
                    { new Guid("a0000006-0000-0000-0000-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Medical care for infants and children", new DateOnly(2008, 6, 20), false, "Pediatrics", null },
                    { new Guid("a0000006-0000-0000-0000-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Bones, joints, and musculoskeletal care", new DateOnly(2011, 9, 10), false, "Orthopedics", null },
                    { new Guid("a0000006-0000-0000-0000-000000000005"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Primary care and general medicine", new DateOnly(2005, 4, 1), false, "General Practice", null }
                });

            migrationBuilder.InsertData(
                table: "Employees",
                columns: new[] { "Id", "CreatedAt", "HireDate", "IsDeleted", "ManagerId", "PersonId", "Status", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("a0000003-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2022, 2, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000001"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2022, 3, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000002"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2022, 4, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000003"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2022, 5, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000004"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000005"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2022, 6, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000005"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000006"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2022, 7, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000006"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000007"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2022, 8, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000007"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000008"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2022, 9, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000008"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000009"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2022, 10, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000009"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000010"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2022, 11, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000010"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000011"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2022, 12, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000011"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000012"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2023, 1, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000012"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000013"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2023, 2, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000013"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000014"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2023, 3, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000014"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000015"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2023, 4, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000015"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000016"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2023, 5, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000016"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000017"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2023, 6, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000017"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000018"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2023, 7, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000018"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000019"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2023, 8, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000019"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000020"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2023, 9, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000020"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000021"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2023, 10, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000021"), "Active", null },
                    { new Guid("a0000003-0000-0000-0000-000000000022"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateOnly(2023, 11, 15), false, null, new Guid("a0000001-0000-0000-0000-000000000022"), "Active", null }
                });

            migrationBuilder.InsertData(
                table: "Patients",
                columns: new[] { "Id", "CreatedAt", "EmergencyContact", "InsuranceNumber", "IsDeleted", "PersonId", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("a0000005-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000001", "BHXH-000001", false, new Guid("a0000001-0000-0000-0000-000000000023"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000002", "BHXH-000002", false, new Guid("a0000001-0000-0000-0000-000000000024"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000003", "BHXH-000003", false, new Guid("a0000001-0000-0000-0000-000000000025"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000004", "BHXH-000004", false, new Guid("a0000001-0000-0000-0000-000000000026"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000005"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000005", "BHXH-000005", false, new Guid("a0000001-0000-0000-0000-000000000027"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000006"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000006", "BHXH-000006", false, new Guid("a0000001-0000-0000-0000-000000000028"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000007"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000007", "BHXH-000007", false, new Guid("a0000001-0000-0000-0000-000000000029"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000008"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000008", "BHXH-000008", false, new Guid("a0000001-0000-0000-0000-000000000030"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000009"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000009", "BHXH-000009", false, new Guid("a0000001-0000-0000-0000-000000000031"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000010"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000010", "BHXH-000010", false, new Guid("a0000001-0000-0000-0000-000000000032"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000011"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000011", "BHXH-000011", false, new Guid("a0000001-0000-0000-0000-000000000033"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000012"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000012", "BHXH-000012", false, new Guid("a0000001-0000-0000-0000-000000000034"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000013"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000013", "BHXH-000013", false, new Guid("a0000001-0000-0000-0000-000000000035"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000014"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000014", "BHXH-000014", false, new Guid("a0000001-0000-0000-0000-000000000036"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000015"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000015", "BHXH-000015", false, new Guid("a0000001-0000-0000-0000-000000000037"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000016"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000016", "BHXH-000016", false, new Guid("a0000001-0000-0000-0000-000000000038"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000017"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000017", "BHXH-000017", false, new Guid("a0000001-0000-0000-0000-000000000039"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000018"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000018", "BHXH-000018", false, new Guid("a0000001-0000-0000-0000-000000000040"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000019"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000019", "BHXH-000019", false, new Guid("a0000001-0000-0000-0000-000000000041"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000020"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000020", "BHXH-000020", false, new Guid("a0000001-0000-0000-0000-000000000042"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000021"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000021", "BHXH-000021", false, new Guid("a0000001-0000-0000-0000-000000000043"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000022"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000022", "BHXH-000022", false, new Guid("a0000001-0000-0000-0000-000000000044"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000023"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000023", "BHXH-000023", false, new Guid("a0000001-0000-0000-0000-000000000045"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000024"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000024", "BHXH-000024", false, new Guid("a0000001-0000-0000-0000-000000000046"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000025"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000025", "BHXH-000025", false, new Guid("a0000001-0000-0000-0000-000000000047"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000026"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000026", "BHXH-000026", false, new Guid("a0000001-0000-0000-0000-000000000048"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000027"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000027", "BHXH-000027", false, new Guid("a0000001-0000-0000-0000-000000000049"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000028"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000028", "BHXH-000028", false, new Guid("a0000001-0000-0000-0000-000000000050"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000029"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000029", "BHXH-000029", false, new Guid("a0000001-0000-0000-0000-000000000051"), null },
                    { new Guid("a0000005-0000-0000-0000-000000000030"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0908000030", "BHXH-000030", false, new Guid("a0000001-0000-0000-0000-000000000052"), null }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "IsActive", "IsDeleted", "PasswordHash", "PersonId", "UpdatedAt", "Username" },
                values: new object[,]
                {
                    { new Guid("a0000002-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000001"), null, "doctor1" },
                    { new Guid("a0000002-0000-0000-0000-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000002"), null, "doctor2" },
                    { new Guid("a0000002-0000-0000-0000-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000003"), null, "doctor3" },
                    { new Guid("a0000002-0000-0000-0000-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000004"), null, "doctor4" },
                    { new Guid("a0000002-0000-0000-0000-000000000005"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000005"), null, "doctor5" },
                    { new Guid("a0000002-0000-0000-0000-000000000006"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000006"), null, "doctor6" },
                    { new Guid("a0000002-0000-0000-0000-000000000007"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000007"), null, "doctor7" },
                    { new Guid("a0000002-0000-0000-0000-000000000008"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000008"), null, "doctor8" },
                    { new Guid("a0000002-0000-0000-0000-000000000009"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000009"), null, "doctor9" },
                    { new Guid("a0000002-0000-0000-0000-000000000010"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000010"), null, "doctor10" },
                    { new Guid("a0000002-0000-0000-0000-000000000011"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000011"), null, "doctor11" },
                    { new Guid("a0000002-0000-0000-0000-000000000012"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000012"), null, "doctor12" },
                    { new Guid("a0000002-0000-0000-0000-000000000013"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000013"), null, "doctor13" },
                    { new Guid("a0000002-0000-0000-0000-000000000014"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000014"), null, "doctor14" },
                    { new Guid("a0000002-0000-0000-0000-000000000015"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000015"), null, "doctor15" },
                    { new Guid("a0000002-0000-0000-0000-000000000016"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000016"), null, "admin1" },
                    { new Guid("a0000002-0000-0000-0000-000000000017"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000017"), null, "admin2" },
                    { new Guid("a0000002-0000-0000-0000-000000000018"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000018"), null, "receptionist1" },
                    { new Guid("a0000002-0000-0000-0000-000000000019"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000019"), null, "receptionist2" },
                    { new Guid("a0000002-0000-0000-0000-000000000020"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000020"), null, "receptionist3" },
                    { new Guid("a0000002-0000-0000-0000-000000000021"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000021"), null, "receptionist4" },
                    { new Guid("a0000002-0000-0000-0000-000000000022"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000022"), null, "receptionist5" },
                    { new Guid("a0000002-0000-0000-0000-000000000023"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000023"), null, "patient1" },
                    { new Guid("a0000002-0000-0000-0000-000000000024"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000024"), null, "patient2" },
                    { new Guid("a0000002-0000-0000-0000-000000000025"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000025"), null, "patient3" },
                    { new Guid("a0000002-0000-0000-0000-000000000026"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000026"), null, "patient4" },
                    { new Guid("a0000002-0000-0000-0000-000000000027"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000027"), null, "patient5" },
                    { new Guid("a0000002-0000-0000-0000-000000000028"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000028"), null, "patient6" },
                    { new Guid("a0000002-0000-0000-0000-000000000029"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000029"), null, "patient7" },
                    { new Guid("a0000002-0000-0000-0000-000000000030"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000030"), null, "patient8" },
                    { new Guid("a0000002-0000-0000-0000-000000000031"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000031"), null, "patient9" },
                    { new Guid("a0000002-0000-0000-0000-000000000032"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000032"), null, "patient10" },
                    { new Guid("a0000002-0000-0000-0000-000000000033"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000033"), null, "patient11" },
                    { new Guid("a0000002-0000-0000-0000-000000000034"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000034"), null, "patient12" },
                    { new Guid("a0000002-0000-0000-0000-000000000035"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000035"), null, "patient13" },
                    { new Guid("a0000002-0000-0000-0000-000000000036"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000036"), null, "patient14" },
                    { new Guid("a0000002-0000-0000-0000-000000000037"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000037"), null, "patient15" },
                    { new Guid("a0000002-0000-0000-0000-000000000038"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000038"), null, "patient16" },
                    { new Guid("a0000002-0000-0000-0000-000000000039"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000039"), null, "patient17" },
                    { new Guid("a0000002-0000-0000-0000-000000000040"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000040"), null, "patient18" },
                    { new Guid("a0000002-0000-0000-0000-000000000041"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000041"), null, "patient19" },
                    { new Guid("a0000002-0000-0000-0000-000000000042"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000042"), null, "patient20" },
                    { new Guid("a0000002-0000-0000-0000-000000000043"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000043"), null, "patient21" },
                    { new Guid("a0000002-0000-0000-0000-000000000044"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000044"), null, "patient22" },
                    { new Guid("a0000002-0000-0000-0000-000000000045"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000045"), null, "patient23" },
                    { new Guid("a0000002-0000-0000-0000-000000000046"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000046"), null, "patient24" },
                    { new Guid("a0000002-0000-0000-0000-000000000047"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000047"), null, "patient25" },
                    { new Guid("a0000002-0000-0000-0000-000000000048"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000048"), null, "patient26" },
                    { new Guid("a0000002-0000-0000-0000-000000000049"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000049"), null, "patient27" },
                    { new Guid("a0000002-0000-0000-0000-000000000050"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000050"), null, "patient28" },
                    { new Guid("a0000002-0000-0000-0000-000000000051"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000051"), null, "patient29" },
                    { new Guid("a0000002-0000-0000-0000-000000000052"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K", new Guid("a0000001-0000-0000-0000-000000000052"), null, "patient30" }
                });

            migrationBuilder.InsertData(
                table: "Doctors",
                columns: new[] { "Id", "Biography", "CreatedAt", "EmployeeId", "ExperienceYears", "IsDeleted", "LicenseNumber", "Qualification", "Status", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("a0000004-0000-0000-0000-000000000001"), "Sample profile for Nguyen Van An.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000001"), 6, false, "LIC-0001", "MD, Cardiology Specialist I", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000002"), "Sample profile for Tran Thi Binh.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000002"), 7, false, "LIC-0002", "MD, Cardiology Specialist II", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000003"), "Sample profile for Le Minh Chau.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000003"), 8, false, "LIC-0003", "MD, Interventional Cardiology", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000004"), "Sample profile for Pham Quoc Dung.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000004"), 9, false, "LIC-0004", "MD, Dermatology Specialist I", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000005"), "Sample profile for Hoang Thi Em.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000005"), 10, false, "LIC-0005", "MD, Cosmetic Dermatology", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000006"), "Sample profile for Vu Van Phuc.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000006"), 11, false, "LIC-0006", "MD, Dermatology Specialist II", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000007"), "Sample profile for Dang Thi Giang.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000007"), 12, false, "LIC-0007", "MD, Pediatrics Specialist I", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000008"), "Sample profile for Bui Van Hai.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000008"), 13, false, "LIC-0008", "MD, Neonatology", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000009"), "Sample profile for Ngo Thi Hoa.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000009"), 14, false, "LIC-0009", "MD, Pediatrics Specialist II", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000010"), "Sample profile for Duong Van Khoa.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000010"), 15, false, "LIC-0010", "MD, Orthopedic Surgery", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000011"), "Sample profile for Luong Thi Lan.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000011"), 16, false, "LIC-0011", "MD, Sports Medicine", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000012"), "Sample profile for Trinh Van Minh.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000012"), 17, false, "LIC-0012", "MD, Trauma Orthopedics", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000013"), "Sample profile for Cao Thi Nga.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000013"), 18, false, "LIC-0013", "MD, Family Medicine", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000014"), "Sample profile for Phan Van Quang.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000014"), 19, false, "LIC-0014", "MD, Internal Medicine", "Active", null },
                    { new Guid("a0000004-0000-0000-0000-000000000015"), "Sample profile for Do Thi Quyen.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000003-0000-0000-0000-000000000015"), 20, false, "LIC-0015", "MD, General Practice", "Active", null }
                });

            migrationBuilder.InsertData(
                table: "UserRoles",
                columns: new[] { "RoleId", "UserId" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000002") },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000002") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000004") },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000004") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000005") },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000005") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000006") },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000006") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000007") },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000007") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000008") },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000008") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000009") },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000009") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000010") },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000010") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000011") },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000011") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000012") },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000012") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000013") },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000013") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000014") },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000014") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000015") },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000015") },
                    { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("a0000002-0000-0000-0000-000000000016") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000016") },
                    { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("a0000002-0000-0000-0000-000000000017") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000017") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000018") },
                    { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("a0000002-0000-0000-0000-000000000018") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000019") },
                    { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("a0000002-0000-0000-0000-000000000019") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000020") },
                    { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("a0000002-0000-0000-0000-000000000020") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000021") },
                    { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("a0000002-0000-0000-0000-000000000021") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000022") },
                    { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("a0000002-0000-0000-0000-000000000022") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000023") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000024") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000025") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000026") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000027") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000028") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000029") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000030") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000031") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000032") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000033") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000034") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000035") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000036") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000037") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000038") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000039") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000040") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000041") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000042") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000043") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000044") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000045") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000046") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000047") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000048") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000049") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000050") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000051") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000052") }
                });

            migrationBuilder.InsertData(
                table: "WorkHistories",
                columns: new[] { "Id", "CreatedAt", "DoctorId", "EndDate", "IsDeleted", "SpecialtyId", "StartDate", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("a0000007-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000001"), null, false, new Guid("a0000006-0000-0000-0000-000000000001"), new DateOnly(2022, 2, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000002"), null, false, new Guid("a0000006-0000-0000-0000-000000000001"), new DateOnly(2022, 3, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000003"), null, false, new Guid("a0000006-0000-0000-0000-000000000001"), new DateOnly(2022, 4, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000004"), null, false, new Guid("a0000006-0000-0000-0000-000000000002"), new DateOnly(2022, 5, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000005"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000005"), null, false, new Guid("a0000006-0000-0000-0000-000000000002"), new DateOnly(2022, 6, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000006"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000006"), null, false, new Guid("a0000006-0000-0000-0000-000000000002"), new DateOnly(2022, 7, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000007"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000007"), null, false, new Guid("a0000006-0000-0000-0000-000000000003"), new DateOnly(2022, 8, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000008"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000008"), null, false, new Guid("a0000006-0000-0000-0000-000000000003"), new DateOnly(2022, 9, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000009"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000009"), null, false, new Guid("a0000006-0000-0000-0000-000000000003"), new DateOnly(2022, 10, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000010"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000010"), null, false, new Guid("a0000006-0000-0000-0000-000000000004"), new DateOnly(2022, 11, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000011"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000011"), null, false, new Guid("a0000006-0000-0000-0000-000000000004"), new DateOnly(2022, 12, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000012"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000012"), null, false, new Guid("a0000006-0000-0000-0000-000000000004"), new DateOnly(2023, 1, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000013"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000013"), null, false, new Guid("a0000006-0000-0000-0000-000000000005"), new DateOnly(2023, 2, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000014"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000014"), null, false, new Guid("a0000006-0000-0000-0000-000000000005"), new DateOnly(2023, 3, 15), null },
                    { new Guid("a0000007-0000-0000-0000-000000000015"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("a0000004-0000-0000-0000-000000000015"), null, false, new Guid("a0000006-0000-0000-0000-000000000005"), new DateOnly(2023, 4, 15), null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000016"));

            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000017"));

            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000018"));

            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000019"));

            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000020"));

            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000021"));

            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000022"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000015"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000016"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000017"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000018"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000019"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000020"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000021"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000022"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000023"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000024"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000025"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000026"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000027"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000028"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000029"));

            migrationBuilder.DeleteData(
                table: "Patients",
                keyColumn: "Id",
                keyValue: new Guid("a0000005-0000-0000-0000-000000000030"));

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000004") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000004") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000005") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000005") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000006") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000006") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000007") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000007") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000008") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000008") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000009") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000009") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000010") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000010") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000011") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000011") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000012") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000012") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000013") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000013") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000014") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000014") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000015") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000015") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("a0000002-0000-0000-0000-000000000016") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000016") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("a0000002-0000-0000-0000-000000000017") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000017") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000018") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("a0000002-0000-0000-0000-000000000018") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000019") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("a0000002-0000-0000-0000-000000000019") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000020") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("a0000002-0000-0000-0000-000000000020") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000021") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("a0000002-0000-0000-0000-000000000021") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000022") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("a0000002-0000-0000-0000-000000000022") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000023") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000024") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000025") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000026") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000027") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000028") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000029") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000030") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000031") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000032") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000033") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000034") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000035") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000036") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000037") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000038") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000039") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000040") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000041") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000042") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000043") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000044") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000045") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000046") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000047") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000048") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000049") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000050") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000051") });

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("a0000002-0000-0000-0000-000000000052") });

            migrationBuilder.DeleteData(
                table: "WorkHistories",
                keyColumn: "Id",
                keyValue: new Guid("a0000007-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "WorkHistories",
                keyColumn: "Id",
                keyValue: new Guid("a0000007-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "WorkHistories",
                keyColumn: "Id",
                keyValue: new Guid("a0000007-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "WorkHistories",
                keyColumn: "Id",
                keyValue: new Guid("a0000007-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "WorkHistories",
                keyColumn: "Id",
                keyValue: new Guid("a0000007-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "WorkHistories",
                keyColumn: "Id",
                keyValue: new Guid("a0000007-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "WorkHistories",
                keyColumn: "Id",
                keyValue: new Guid("a0000007-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "WorkHistories",
                keyColumn: "Id",
                keyValue: new Guid("a0000007-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "WorkHistories",
                keyColumn: "Id",
                keyValue: new Guid("a0000007-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "WorkHistories",
                keyColumn: "Id",
                keyValue: new Guid("a0000007-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "WorkHistories",
                keyColumn: "Id",
                keyValue: new Guid("a0000007-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "WorkHistories",
                keyColumn: "Id",
                keyValue: new Guid("a0000007-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "WorkHistories",
                keyColumn: "Id",
                keyValue: new Guid("a0000007-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                table: "WorkHistories",
                keyColumn: "Id",
                keyValue: new Guid("a0000007-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                table: "WorkHistories",
                keyColumn: "Id",
                keyValue: new Guid("a0000007-0000-0000-0000-000000000015"));

            migrationBuilder.DeleteData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: new Guid("a0000004-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: new Guid("a0000004-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: new Guid("a0000004-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: new Guid("a0000004-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: new Guid("a0000004-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: new Guid("a0000004-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: new Guid("a0000004-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: new Guid("a0000004-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: new Guid("a0000004-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: new Guid("a0000004-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: new Guid("a0000004-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: new Guid("a0000004-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: new Guid("a0000004-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: new Guid("a0000004-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                table: "Doctors",
                keyColumn: "Id",
                keyValue: new Guid("a0000004-0000-0000-0000-000000000015"));

            migrationBuilder.DeleteData(
                table: "Specialties",
                keyColumn: "Id",
                keyValue: new Guid("a0000006-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Specialties",
                keyColumn: "Id",
                keyValue: new Guid("a0000006-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Specialties",
                keyColumn: "Id",
                keyValue: new Guid("a0000006-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Specialties",
                keyColumn: "Id",
                keyValue: new Guid("a0000006-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Specialties",
                keyColumn: "Id",
                keyValue: new Guid("a0000006-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000015"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000016"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000017"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000018"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000019"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000020"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000021"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000022"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000023"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000024"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000025"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000026"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000027"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000028"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000029"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000030"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000031"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000032"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000033"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000034"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000035"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000036"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000037"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000038"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000039"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000040"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000041"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000042"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000043"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000044"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000045"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000046"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000047"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000048"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000049"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000050"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000051"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000052"));

            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "Id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000015"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000016"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000017"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000018"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000019"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000020"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000021"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000022"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000023"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000024"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000025"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000026"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000027"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000028"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000029"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000030"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000031"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000032"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000033"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000034"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000035"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000036"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000037"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000038"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000039"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000040"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000041"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000042"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000043"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000044"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000045"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000046"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000047"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000048"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000049"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000050"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000051"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000052"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                table: "Persons",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000015"));
        }
    }
}
