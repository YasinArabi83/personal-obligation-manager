# Personal Obligation Manager — Product & Technical Specification (v1.0)
> بازار هدف: ایران | Web-first, No-AI MVP | نویسنده: Claude (در نقش Senior PM + BA + Solution Architect + AI Agent Architect)

---

## 0. سؤالات Critical و فرض‌های من

قبل از شروع، چند نکته وجود دارد که نمی‌توانم بدون اطلاعات بیشتر تصمیم قطعی بگیرم. برای هرکدام یک فرض منطقی گذاشتم و بر همان اساس ادامه دادم. اگر فرض اشتباه است، فقط همین بخش نیاز به بازبینی معماری دارد، نه کل سند.

| # | سؤال | فرض من برای ادامه کار |
|---|------|------------------------|
| Q1 | آیا محصول **تک‌کاربره** است یا نیاز به **اشتراک‌گذاری بین اعضای خانواده** دارد (مثلاً همسر هم ببیند)؟ | **تک‌کاربره در MVP.** Multi-user/family sharing به Post-MVP موکول شد؛ اما مدل داده طوری طراحی می‌شود که افزودنش بعداً breaking نباشد (هر Obligation از ابتدا owner_id دارد). |
| Q2 | مدل درآمدی چیست؟ (رایگان / Freemium / اشتراک) | فرض: **بدون تصمیم قطعی**، اما یک فیلد Quota ساده (تعداد فایل/حجم) در معماری در نظر گرفته می‌شود تا بعداً محدودسازی پلن‌ها راحت باشد. |
| Q3 | زیرساخت (Hosting) کجا خواهد بود؟ سرویس‌های ابری بین‌المللی (AWS/Azure/GCP) در ایران با محدودیت دسترسی مواجه‌اند و سرویس‌های ایمیل/SMS بین‌المللی هم ریسک تحریمی دارند. | فرض: باید معماری **Cloud-agnostic و قابل Self-host روی یک VPS ساده (Docker Compose)** باشد تا هم روی ابر خارجی و هم روی زیرساخت داخلی (مثل ArvanCloud) قابل استقرار باشد. جزئیات در بخش ۳۳. |
| Q4 | ورود با Google/Social Login لازم است؟ | فرض: **خیر در MVP** (به دلیل ریسک دسترسی به Google APIs از ایران و پیچیدگی اضافه). فقط Email + Password. |
| Q5 | مقیاس کاربران اولیه چقدر است؟ | فرض: چند هزار کاربر در سال اول → **Modular Monolith** کاملاً کافی است؛ Microservices توجیه ندارد. |

---

## 1. Product Vision

> **«یک سیستم شخصی که هیچ تعهد، سررسید، تمدید، پرداخت یا کار مهمی را از قلم نمی‌اندازد — مخصوص واقعیت زندگی اداری/مالی ایرانی.»**

Personal Obligation Manager یک لایه یکپارچه بالای زندگی شخصی است: نه یک Todo ساده، بلکه یک **Obligation Tracker** که مفهوم «چیزی که باید در زمان مشخص یا در پاسخ یک رویداد انجام شود» را مرکز محصول قرار می‌دهد.

## 2. Target Users

- بزرگسالان شهری ۲۵–۵۰ سال، شاغل، دارای حداقل یکی از: خودرو، وام/قسط، بیمه، اجاره، اشتراک‌های متعدد.
- کاربرانی که در حال حاضر از ترکیب «حافظه + کاغذ + یادداشت گوشی + پیام تلگرام به خودشان» استفاده می‌کنند و حداقل یک‌بار سررسید مهمی (چک، بیمه، معاینه فنی) را از دست داده‌اند.
- در فاز اول: کاربران Tech-comfortable که با Web App راحت هستند (نه لزوماً Tech-savvy).

## 3. User Problems

- تعهدات در Iran عمدتاً **پراکنده و ناهمگون** هستند: بعضی تاریخ شمسی دارند (بیمه، معاینه فنی)، بعضی چرخه‌ای نامنظم (چک ماهانه با تاریخ متغیر)، بعضی وابسته به رویداد (پس از تحویل خودرو، ۶ ماه بعد سرویس).
- ابزارهای عمومی (Google Calendar, Todoist) برای Reminder ساده خوبند اما مفهوم «تعهد با وضعیت، سابقه، سند پیوست، و ارتباط با شخص/دارایی» را ندارند.
- کاربر ایرانی معمولاً چند سررسید مالی هم‌زمان دارد (قسط، اجاره، شارژ، بدهی) که با پول (تومان) و طرف حساب (شخص) گره خورده و در Todo List عادی گم می‌شود.

## 4. Core Value Proposition

یک محل واحد که:
1. هر نوع تعهد (زمان‌بندی‌شده، دوره‌ای، رویدادمحور) را با **یک مدل داده منعطف** می‌پذیرد.
2. تقویم شمسی و تومان را **به‌صورت native** پشتیبانی می‌کند (نه Add-on).
3. تعهد را به شخص/دارایی مرتبط می‌کند تا سابقه معنادار شکل بگیرد (مثلاً «تاریخچه سرویس خودرو من»).
4. بدون وابستگی به AI، صرفاً با قوانین Recurrence و Reminder قدرتمند، چیزی را از قلم نمی‌اندازد.

## 5. Product Scope

**در Scope (نهایتاً، نه لزوماً MVP):** Obligation tracking، Recurrence، Reminder/Notification، Document attachment، Asset linking، Dashboard، Search/Filter، Persian calendar.

**خارج از Scope (همیشه):**
- محصول **Accounting/Bookkeeping** نمی‌شود (بدون گزارش سود/زیان، بدون double-entry، بدون بانک‌کانکت).
- محصول **CRM** نمی‌شود.
- در MVP: بدون AI، بدون Native App، بدون Multi-user/Sharing، بدون SMS.

## 6. Core Concepts — Entity واحد یا چند Entity؟

این مهم‌ترین تصمیم معماری محصول است.

**تصمیم: یک Entity مرکزی به نام `Obligation` با فیلد `Type` (enum) به‌جای ۱۰+ Entity جدا (Payment, Contract, Document, Subscription و…).**

دلایل:
- اکثر «انواع» که در لیست شما آمده (تمدید بیمه، پرداخت قسط، پایان قرارداد، تمدید گواهینامه...) از نظر رفتاری **یک چیز مشترک** دارند: عنوان + تاریخ سررسید (یا بازه) + وضعیت + Recurrence اختیاری + Reminder + پیوست + دسته‌بندی. تفاوت‌شان صرفاً در چند فیلد اضافی (مبلغ برای Payment، طرف قرارداد برای Contract) است.
- ساختن Entity جدا برای هرکدام یعنی ۱۵ جدول، ۱۵ ست CRUD API، ۱۵ فرم UI → دقیقاً همان Overengineering که خودتان نگرانش هستید و برای یک Developer + AI Agent در MVP قابل مدیریت نیست.
- راه‌حل: `Obligation` هسته‌ی مشترک + یک بلوک `ExtraFields` (JSONB در Postgres) برای داده‌های type-specific (مثلاً `amount`, `counterpartyId`, `installmentNo`). این یعنی **schema-flexible بدون نیاز به Entity جدید برای هر نوع جدید در آینده**.
- `Person` و `Asset` به‌عنوان Entity جدا باقی می‌مانند چون رابطه (Relationship)، نه یک «نوع تعهد» هستند — یک Obligation می‌تواند به یک Person یا Asset لینک شود.

نتیجه: **Multi-Entity محدود، نه Single-Generic-Entity کامل، نه Many-Entity کامل.** تعادل.

## 7. Domain Model

### Entityهای MVP (ضروری)
```
User
Obligation            (هسته اصلی – شامل Type, Title, Dates, Status, Priority, ExtraFields JSON)
RecurrenceRule         (1-1 با Obligation، فقط اگر تکرارشونده باشد)
ObligationOccurrence    (نمونه‌های تولیدشده از یک Obligation تکرارشونده – برای تاریخچه/تکمیل)
Reminder                (چند به یک با Obligation)
Category                (کاربر ساخته یا پیش‌فرض)
Tag / ObligationTag     (many-to-many)
Attachment               (فایل پیوست به Obligation)
ActivityLog              (تاریخچه تغییرات یک Obligation)
NotificationLog           (رکورد ارسال هر Reminder – برای جلوگیری از ارسال تکراری)
```

### Entityهای Should-Have (سبک نگه داشته می‌شوند)
```
Person       (طرف حساب برای بدهی/طلب – فقط Name + Phone + Note)
Asset        (خودرو/لپ‌تاپ/ملک – فقط Name + Type + Metadata ساده)
```

### Entityهای Later (به‌صراحت در MVP ساخته نمی‌شوند)
```
Organization (جدا از Person)
Event (به‌عنوان Entity مستقل از Trigger)
NotificationChannel / NotificationTemplate / NotificationSchedule (به‌صورت Entity جدا – در MVP این‌ها Config در کد هستند نه دیتابیس)
Contract به‌عنوان Entity جدا (در MVP، Contract فقط یک Type از Obligation است)
Document به‌عنوان Entity جدا (Attachment کافی است)
```

### رابطه‌ها (خلاصه)
```
User 1---* Obligation
Obligation 1---0..1 RecurrenceRule
Obligation 1---* ObligationOccurrence   (فقط برای recurring)
Obligation 1---* Reminder
Obligation 1---* Attachment
Obligation *---* Tag
Obligation *---1 Category
Obligation *---0..1 Person   (nullable FK)
Obligation *---0..1 Asset    (nullable FK)
```

## 8. Functional Requirements (با اولویت)

| Feature | اولویت |
|---|---|
| Auth (ثبت‌نام/ورود Email+Password) | MUST |
| Profile ساده (نام، ایمیل، تنظیمات تقویم) | MUST |
| Dashboard (Today/Week/Month/Overdue/Upcoming) | MUST |
| Create/Edit/Delete(Soft)/Archive Obligation | MUST |
| Complete / Postpone / Skip | MUST |
| One-time Obligation | MUST |
| Recurring Obligation (پترن‌های اصلی) | MUST |
| Reminder چندگانه per Obligation | MUST |
| Category (پیش‌فرض + کاربرساخته) | MUST |
| Tag | SHOULD |
| Notes (متن آزاد) | MUST |
| Attachment (فایل) | MUST |
| Search (عنوان/توضیح/تگ) | MUST |
| Filter (نوع/دسته/وضعیت/بازه تاریخ) | MUST |
| Sort | MUST |
| List View | MUST |
| Calendar View | SHOULD |
| Timeline View | LATER |
| Persian Calendar (نمایش+ورودی) | MUST |
| Gregorian Calendar (toggle) | MUST |
| Toman/Rial | MUST |
| Email Notification | MUST |
| Browser (Web Push) Notification | SHOULD |
| SMS Notification | LATER |
| Person entity (برای بدهی/طلب) | SHOULD |
| Asset entity (خودرو/لپ‌تاپ...) | SHOULD |
| Activity Log per Obligation | SHOULD |
| History کامل تکرارها | SHOULD |
| Data Export (JSON/CSV) | SHOULD |
| Data Backup (خودکار سمت سرور) | SHOULD |
| Account Deletion (GDPR-like) | MUST |
| Timezone handling | MUST (ثابت Asia/Tehran در MVP) |
| Multi-user/Sharing | LATER |
| Native App | LATER |
| AI هر نوع | LATER (خارج از Roadmap فعلی) |

## 9. Non-Functional Requirements

- **Performance:** Dashboard باید زیر ۵۰۰ms (p95) برای کاربر با تا ۲۰۰۰ Obligation فعال لود شود.
- **Availability:** برای MVP، uptime هدف ۹۹٪ کافی است (بدون نیاز به Multi-region).
- **Scalability:** معماری باید بتواند بدون rewrite تا ~۵۰,۰۰۰ کاربر فعال روی یک سرور مناسب (Vertical scaling) پاسخگو باشد.
- **Security & Privacy:** رمزنگاری در Transit (HTTPS اجباری)، Isolation کامل داده بین کاربران، رمز عبور Hash شده (bcrypt/argon2)، بدون ذخیره داده حساس غیرضروری.
- **Localization:** فارسی RTL کامل به‌عنوان زبان پیش‌فرض؛ معماری i18n-ready برای افزودن انگلیسی بعداً.
- **Accessibility:** حداقل کنتراست رنگ مناسب، فونت خوانا فارسی (مثل Vazirmatn)، ورودی تاریخ با کیبورد.
- **Portability زیرساخت:** باید با Docker Compose روی هر VPS (داخلی یا خارجی) قابل استقرار باشد — به دلیل ریسک تحریم/فیلترینگ سرویس‌های ابری بین‌المللی.

## 10. MVP Features (خلاصه نهایی و صریح)

**داخل MVP:**
Auth ساده، Obligation CRUD با Type Enum، Recurrence (پترن‌های رایج شمسی/میلادی)، Reminder چندگانه، Email Notification، Category+Tag، Attachment (آپلود ساده)، Search/Filter/Sort، List View، Dashboard (Today/Week/Month/Overdue)، Persian+Gregorian date، Toman، Soft Delete، Export ساده (JSON)، Account Deletion.

**به‌صراحت خارج از MVP:**
- Calendar View گرافیکی کامل (تقویم ماهانه با drag) → به Should/Post-MVP اول موکول شود، چون پیچیدگی UI بالایی دارد نسبت به ارزش MVP.
- Web Push Notification → نیاز به Service Worker + VAPID keys؛ Should-Have، نه Must برای اولین نسخه قابل عرضه.
- Person/Asset entities → می‌توانند در Sprint دوم MVP (نه نسخه صفر) اضافه شوند؛ اگر می‌خواهید MVP واقعاً حداقلی باشد، این دو را به فاز ۲ منتقل کنید (پیشنهاد من در بخش ۳۴).
- SMS، Native App، AI، Multi-user، Accounting features، Versioning فایل.

## 11. Post-MVP Features

Calendar View کامل، Web Push، Person/Asset (اگر در فاز ۱ نبود)، Family Sharing، Recurring "Skip with reason"، Advanced reports (مثل «مجموع اقساط این ماه»)، SMS، PWA installable، AI-assisted categorization/suggestions (فاز بسیار بعدی و کاملاً اختیاری).

## 12. User Flows (نمونه‌های کلیدی)

**Quick Add:**
کاربر روی «+» کلیک می‌کند → یک فیلد متن باز می‌شود («تمدید بیمه خودرو ۱۵ شهریور») → سیستم Title و تاریخ را جدا می‌کند (Parsing ساده Regex-based، نه AI) یا کاربر دستی از Date-picker انتخاب می‌کند → ذخیره در کمتر از ۱۰ ثانیه.

**تکمیل یک تعهد تکرارشونده:**
کاربر روی «انجام شد» کلیک می‌کند → Occurrence جاری Complete می‌شود → سیستم بر اساس RecurrenceRule، Occurrence بعدی را محاسبه و می‌سازد → در ActivityLog ثبت می‌شود.

**Overdue شدن:**
Job روزانه (Background) وضعیت Obligationهای گذشته از due date را به `Overdue` تغییر می‌دهد → در Dashboard در سکشن «موارد عقب‌افتاده» ظاهر می‌شود → اگر Recurring باشد، رفتار بر اساس تنظیم `OnMissed` مشخص می‌شود (بخش ۱۴).

## 13. Dashboard Requirements

سکشن‌های ثابت (بدون شلوغی):
```
امروز | این هفته | این ماه | عقب‌افتاده | آینده (اختیاری/collapsed)
```
هر سکشن حداکثر تعداد کارت مشخص (مثلاً ۵) با لینک «مشاهده همه». Quick Add همیشه بالای صفحه ثابت. هیچ ویجت تزئینی/گزارش گرافیکی در MVP (چارت، درصد پیشرفت و…) — این‌ها Should/Later هستند.

## 14. Recurrence Engine

**مدل داده (RecurrenceRule):**
```
Frequency: Daily | Weekly | Monthly | Yearly | CustomInterval
Interval: int (هر X روز/هفته/ماه/سال)
DayOfMonthRule: enum { FixedDay(n) | LastDayOfMonth | NthWeekday(n, weekday) }
CalendarSystem: Jalali | Gregorian   (پترن بر اساس کدام تقویم محاسبه شود)
StartDate, EndDate (nullable = بی‌نهایت), OccurrenceCount (nullable)
OnMissed: CarryOverAsOverdue | AutoSkipAndCreateNext | RequireManualAction   (پیش‌فرض: CarryOverAsOverdue)
```

پترن‌های پشتیبانی‌شده در MVP: روزانه، هفتگی، ماهانه (روز ثابت)، ماهانه (آخرین روز)، سالانه (سالگرد شمسی یا میلادی)، «هر X روز/هفته/ماه/سال». پترن «اولین شنبه ماه» (Nth weekday) به‌عنوان Should-Have (پیچیدگی محاسباتی بالاتر، کاربرد کمتر در نمونه‌های شما).

**استراتژی تولید Occurrence:** به‌جای تولید همه occurrenceهای آینده، فقط occurrence جاری/بعدی به‌صورت Materialized ذخیره می‌شود (Lazy generation) + یک Background Job شبانه occurrence بعدی را در صورت تکمیل/عبور از سررسید می‌سازد. این از رشد بی‌رویه دیتابیس جلوگیری می‌کند.

**رفتار Missed:** پیش‌فرض `CarryOverAsOverdue` — یعنی اگر تکرارشونده انجام نشود، همان occurrence بدون تغییر تاریخ در حالت Overdue می‌ماند تا کاربر آن را Complete/Skip کند؛ occurrence بعدی فقط پس از این اقدام ساخته می‌شود (برای جلوگیری از انباشت Reminder‌های بی‌معنی).

## 15. Reminder Engine

**مدل داده (Reminder):**
```
ObligationId
OffsetType: BeforeDue | OnDue | AfterDue
OffsetDays: int   (مثلاً 30, 7, 3, 1, 0, -3)
Channel: Email | BrowserPush (Should) | SMS (Later)
```

هر Obligation می‌تواند صفر یا چند Reminder داشته باشد. مقادیر پیش‌فرض پیشنهادی هنگام ساخت Obligation جدید (کاربر می‌تواند حذف/اضافه کند): `7 روز قبل`, `1 روز قبل`, `روز سررسید`.

**رفتار Overdue:** اگر due date بگذرد و Obligation هنوز Pending باشد، یک Reminder اضافی خودکار (نه از پیش تعریف‌شده توسط کاربر، بلکه Job سیستمی) با فاصله هفتگی ارسال می‌شود تا زمانی که کاربر Complete/Skip/Archive کند (حداکثر ۳ بار، برای جلوگیری از اسپم).

**Idempotency:** `NotificationLog` هر (ObligationId, ReminderId, تاریخ ارسال) را ثبت می‌کند تا Background Job در صورت اجرای مجدد، پیام تکراری نفرستد.

## 16. Notification System (ساده‌سازی‌شده)

Entity کامل (Notification / Channel / Template / Schedule) برای MVP overkill است. طراحی ساده‌شده:

```
Reminder (تعریف "چه زمانی")   → از قبل در بخش ۱۵
NotificationLog (رکورد "چه زمانی ارسال شد، موفق بود یا نه، از چه کانالی")
```

Template ایمیل به‌صورت Hard-coded (Razor/HTML template ساده) در کد نگه داشته می‌شود — نه در دیتابیس. Channel هم صرفاً یک Enum است، نه Entity. یک Background Worker (Hosted Service در .NET) هر ۵-۱۵ دقیقه Reminderهای due را چک و ارسال می‌کند.

اگر بعداً کانال جدید (SMS) اضافه شد، فقط یک `INotificationSender` جدید Implement می‌شود — بدون نیاز به تغییر Schema.

## 17. Document Management

| موضوع | تصمیم MVP |
|---|---|
| Fileهای مجاز | PDF, JPG, PNG, HEIC (تبدیل خودکار به JPEG سمت سرور اگر لازم) |
| حداکثر حجم هر فایل | ۱۰ مگابایت |
| حداکثر فایل به ازای هر Obligation | ۵ فایل (قابل تغییر با Quota پلن در آینده) |
| Storage | Abstraction روی `IFileStorage` — پیاده‌سازی پیش‌فرض: Local disk / S3-compatible (MinIO/ArvanCloud Object Storage) پشت یک Interface، تا جابه‌جایی زیرساخت بدون تغییر کد ممکن باشد |
| امنیت | فایل‌ها Private؛ دسترسی فقط از طریق URL موقت امضاشده (Signed URL با انقضای کوتاه) یا از طریق API با Auth |
| Delete | Soft delete (قابل بازیابی ۳۰ روزه) هم‌راستا با خود Obligation |
| Download | مستقیم از طریق Signed URL |
| Versioning | **خیر در MVP** — فایل جدید جایگزین قدیمی می‌شود یا کاربر باید فایل جدید جدا آپلود کند |

## 18. Asset Management

**تصمیم: بله، ولی بسیار سبک، و به‌عنوان Should-Have نه Must (پیشنهاد انتقال به فاز ۲ MVP).**

مدل: `Asset { Id, UserId, Name, Type(enum: Vehicle, Laptop/Device, Property, Appliance, Other), Metadata(JSON اختیاری مثل پلاک/مدل) }`. یک Obligation می‌تواند `AssetId` نال‌پذیر داشته باشد. UI نمایش «صفحه یک Asset با لیست تعهدات مرتبط» یک ویژگی بسیار قوی برای تمایز محصول است اما برای اولین نسخه قابل استفاده ضروری نیست — کاربر می‌تواند Tag یا Category برای همین منظور استفاده کند تا Asset پیاده‌سازی شود.

## 19. Financial Obligation Model

بدون تبدیل به Accounting، فقط فیلدهای لازم روی `ExtraFields` نوع Payment/Debt:

| نوع | فیلدهای حداقلی |
|---|---|
| قسط/وام | Amount, TotalInstallments, CurrentInstallmentNo, DueDayOfMonth |
| بدهی/طلب | Amount, CounterpartyPersonId, Direction(Owe/Owed), DueDate |
| چک | Amount, CounterpartyPersonId, ChequeNumber(optional text), DueDate |
| اجاره | Amount, LandlordPersonId, DueDayOfMonth, LeaseEndDate (این خودش یک Obligation جدا هم هست: «پایان قرارداد») |
| قبض/شارژ | Amount(nullable چون متغیر است), DueDate |
| اشتراک | Amount, BillingCycle(Monthly/Yearly), NextChargeDate |

فیلد `Amount` همیشه `decimal` + `Currency` (پیش‌فرض `IRT`/تومان؛ معماری آماده افزودن ارزهای دیگر). هیچ محاسبه تجمعی خودکار (مثل «مجموع بدهی‌ها») در MVP لازم نیست؛ اگر خواستید ساده‌ترین نسخه‌اش (Sum ساده روی Dashboard) Should-Have است.

## 20. Search & Filtering

Full-text ساده روی `Title + Notes` (Postgres `tsvector` با پشتیبانی فارسی محدود؛ در صورت نیاز به فارسی قوی‌تر، `pg_trgm` برای ILIKE fallback). فیلتر ترکیبی روی: Type, Category, Tag, Status, Priority, بازه تاریخ (شمسی/میلادی)، AssetId/PersonId (اگر فاز ۲ باشند).

## 21. Localization / Persian Requirements

- **تقویم شمسی به‌عنوان پیش‌فرض نمایش و ورودی**، با امکان Toggle به میلادی در تنظیمات کاربر.
- تمام تاریخ‌ها **در دیتابیس به‌صورت UTC/Gregorian ذخیره می‌شوند** (استاندارد و بدون ابهام) و فقط در Presentation Layer به شمسی تبدیل می‌شوند. این تصمیم حیاتی است تا محاسبات Recurrence/Reminder دچار Bug تقویمی نشوند.
- Backend (.NET): از `System.Globalization.PersianCalendar` یا کتابخانه `NodaTime` + PersianCalendar استفاده می‌شود (بدون نیاز به پکیج خارجی برای تبدیل پایه).
- Frontend: کتابخانه `jalaali-js` یا `dayjs` + پلاگین jalali برای Date-picker شمسی.
- **RTL کامل**: جهت صفحه، آیکون‌ها (پیکان‌ها آینه شوند)، فرم‌ها، جدول‌ها.
- فونت فارسی خوانا (Vazirmatn پیشنهاد می‌شود) + اعداد فارسی/انگلیسی قابل تنظیم (پیش‌فرض: ارقام انگلیسی برای جلوگیری از مشکل sort/parsing، با نمایش اختیاری ارقام فارسی).
- واحد پول: **تومان به‌عنوان واحد اصلی نمایش**، اما دیتابیس مبلغ را به «ریال» (کوچک‌ترین واحد رایج در سیستم‌های مالی ایرانی) یا با یک Multiplier ثابت ذخیره می‌کند تا ابهام تومان/ریال از بین برود؛ توصیه: ذخیره به‌صورت `Rial (bigint)`، نمایش با تقسیم بر ۱۰.

## 22. Security Requirements — Threat Model اولیه

| حوزه | تصمیم |
|---|---|
| Authentication | Email+Password، Hash با **argon2id** (یا bcrypt در صورت محدودیت کتابخانه .NET)، Rate-limit روی Login |
| Password policy | حداقل ۸ کاراکتر، بدون نیاز به قوانین پیچیده افراطی؛ چک نسبت به لیست پسوردهای رایج (Have I Been Pwned API در صورت دسترسی، وگرنه skip) |
| Session | JWT کوتاه‌مدت (Access ~۱۵ دقیقه) + Refresh Token (HttpOnly, Secure cookie) |
| Authorization | هر Query در سطح Repository/Service با `UserId` فیلتر می‌شود (Row-level ownership check) — بدون تکیه صرف بر Frontend |
| Data isolation | تست‌های Integration اجباری برای اطمینان از عدم نشت داده بین کاربران (بخش Testing) |
| File access | Signed URL با انقضا؛ هیچ فایل مستقیماً Public نیست |
| Rate limiting | روی Endpointهای حساس (Login, Register, Password reset, Create) |
| CSRF | چون API با JWT در Header کار می‌کند (نه صرفاً Cookie session)، ریسک CSRF کم است؛ برای Cookie-based refresh token از `SameSite=Strict` + Anti-forgery token استفاده می‌شود |
| XSS | Output encoding استاندارد فریم‌ورک فرانت (React/Angular به‌صورت پیش‌فرض encode می‌کنند)؛ Sanitize روی فیلد Notes اگر Rich text مجاز باشد |
| SQL Injection | استفاده از EF Core (Parameterized Queries) — بدون Raw SQL دستی مگر ضروری |
| Sensitive data | عدم ذخیره داده غیرضروری (مثل شماره کارت کامل)؛ اگر شماره چک/کارت ذخیره شد، به‌صورت Masked نمایش داده شود |
| Logging | بدون لاگ کردن پسورد/توکن؛ لاگ Structured (Serilog) |
| Audit | ActivityLog per Obligation (چه کسی چه زمانی چه تغییری داد) |
| Account deletion | Soft delete با Grace period ۳۰ روزه، سپس Hard delete واقعی (شامل فایل‌ها) |
| Backup | Backup شبانه دیتابیس (pg_dump) + نگهداری فایل‌ها در Storage با Redundancy پایه |
| Recovery | مستندسازی Runbook بازیابی از Backup (در `SECURITY.md`) |

## 23. Recommended Architecture

**Modular Monolith با Clean Architecture / Layered Architecture داخل یک Solution.**

دلایل رد Microservices برای MVP: تیم یک نفره + AI Agent، حجم داده متوسط، نیاز اصلی سرعت توسعه و سادگی Deploy، نه مقیاس‌پذیری افقی فوری. Modular Monolith این مزیت را می‌دهد که مرزهای ماژول (Obligations, Users, Notifications, Files) از روز اول رعایت شود و در آینده در صورت نیاز واقعی (نه فرضی) بتوان بخشی را جدا کرد.

لایه‌ها:
```
Domain          (Entities, Value Objects, Domain logic — بدون وابستگی به EF/ASP.NET)
Application     (Use cases / Services, Interfaces مثل IFileStorage, INotificationSender)
Infrastructure  (EF Core, PostgreSQL, File storage impl, Email sender impl)
API             (ASP.NET Core Controllers/Minimal API, DTOs, Auth)
Frontend        (پروژه جدا — SPA)
```

## 24. Recommended Tech Stack

| لایه | انتخاب | توجیه |
|---|---|---|
| Backend | **ASP.NET Core (.NET 8/9)** | مطابق ترجیح شما؛ همچنین بلوغ بالا، Tooling عالی، عملکرد خوب، پشتیبانی طولانی‌مدت. |
| ORM | **EF Core** | استاندارد .NET، سرعت توسعه بالا برای MVP، Migration مدیریت‌شده. |
| Database | **PostgreSQL** (نه SQL Server) | رایگان و بدون Licensing، پشتیبانی بومی JSONB (برای ExtraFields)، به‌راحتی روی هر VPS/داخل ایران self-host می‌شود، اکوسیستم Backup/Tooling رایگان قوی. SQL Server هزینه Licensing و وابستگی بیشتر به اکوسیستم مایکروسافت/ابر دارد که در سناریوی self-host ایرانی مزیتی ایجاد نمی‌کند. |
| Frontend | **React + TypeScript + Vite** (پیشنهاد اول) — Angular به‌عنوان جایگزین قابل قبول | React برای MVP سریع‌تر توسعه می‌یابد، اکوسیستم کامپوننت (shadcn/ui, MUI, react-hook-form) بزرگ‌تر است، و AI Coding Agentها معمولاً روی React/TS دقت بالاتری دارند (حجم Training data بیشتر). Angular ساختار قوی‌تری تحمیل می‌کند که با انضباط .NET همخوانی خوبی دارد و اگر شما با آن راحت‌ترید، انتخاب کاملاً معتبری است — تصمیم نهایی را به تجربه شخصی شما واگذار می‌کنم؛ این سند برای هر دو قابل استفاده است. |
| Styling | Tailwind CSS (با پشتیبانی RTL) | سرعت توسعه UI بالا، سازگار با هر دو فریم‌ورک فرانت. |
| Auth | ASP.NET Core Identity + JWT | استاندارد، امن، بدون نیاز به سرویس ثالث. |
| Background Jobs | Hosted Service داخلی .NET (`IHostedService`) یا **Hangfire** (اگر نیاز به UI مانیتورینگ Job‌ها بود) | برای MVP با حجم کم، Hosted Service ساده کافی است؛ Hangfire اگر می‌خواهید Retry/Dashboard آماده داشته باشید. |
| File Storage | Local disk (MVP اول) → S3-compatible (MinIO / ArvanCloud Object Storage) پشت `IFileStorage` | Portability و آمادگی برای ایران. |
| Email | SMTP قابل تعویض (Provider-agnostic) پشت `IEmailSender` | چون سرویس‌های بین‌المللی (SendGrid و…) ریسک تحریمی/فیلترینگ دارند، باید بتوان به‌راحتی به یک SMTP relay داخلی سوییچ کرد. |
| Hosting | Docker Compose روی یک VPS (اروپا یا ایران) | ساده، ارزان، Portable. |

## 25. Database Design (خلاصه Schema)

```sql
users (id, email, password_hash, display_name, calendar_pref, created_at, deleted_at)

obligations (
  id, user_id, type,               -- enum: Task, Payment, Document, Subscription, Contract, Debt, Maintenance, Appointment, Custom
  title, notes,
  start_date, due_date, end_date,  -- همه به Gregorian/UTC
  status,                          -- Pending, Completed, Skipped, Overdue, Archived
  priority,                        -- Low, Medium, High
  category_id, person_id (nullable), asset_id (nullable),
  extra_fields JSONB,              -- amount, currency, installment_no, ...
  is_recurring bool,
  created_at, updated_at, deleted_at
)

recurrence_rules (
  id, obligation_id, frequency, interval, day_of_month_rule,
  calendar_system, start_date, end_date, occurrence_count, on_missed
)

obligation_occurrences (
  id, obligation_id, due_date, status, completed_at
)

reminders (
  id, obligation_id, offset_type, offset_days, channel
)

notification_log (
  id, reminder_id, obligation_id, sent_at, channel, status
)

categories (id, user_id nullable, name, is_default, icon)
tags (id, user_id, name)
obligation_tags (obligation_id, tag_id)

attachments (id, obligation_id, file_name, storage_key, mime_type, size_bytes, uploaded_at, deleted_at)

people (id, user_id, name, phone, note)
assets (id, user_id, name, type, metadata JSONB)

activity_log (id, obligation_id, user_id, action, changes JSONB, created_at)
```

نکته طراحی: `extra_fields JSONB` باعث می‌شود افزودن نوع جدید تعهد در آینده (مثلاً «سالگرد») نیاز به Migration جدید نداشته باشد — فقط منطق Validation در Application Layer اضافه می‌شود.

## 26. API Design

REST، نسخه‌بندی در مسیر (`/api/v1/...`)، خروجی JSON با پوشش خطا استاندارد (`{ error: { code, message } }`).

نمونه Endpointها:
```
POST   /api/v1/auth/register
POST   /api/v1/auth/login
POST   /api/v1/auth/refresh

GET    /api/v1/obligations?status=&category=&from=&to=&q=
POST   /api/v1/obligations
GET    /api/v1/obligations/{id}
PUT    /api/v1/obligations/{id}
DELETE /api/v1/obligations/{id}          -- soft delete
POST   /api/v1/obligations/{id}/complete
POST   /api/v1/obligations/{id}/postpone
POST   /api/v1/obligations/{id}/skip

GET    /api/v1/dashboard                 -- aggregated Today/Week/Month/Overdue

POST   /api/v1/obligations/{id}/attachments
GET    /api/v1/attachments/{id}/download-url

GET    /api/v1/categories
GET    /api/v1/tags
```
Pagination: `?page=&pageSize=` روی Listها. Filtering با Query Params ساده (بدون نیاز به GraphQL/OData در MVP).

## 27. Frontend Architecture

SPA با React+TS (یا Angular). ساختار Feature-based (نه Type-based):
```
/src
  /features/obligations
  /features/dashboard
  /features/auth
  /features/categories
  /shared (ui components, hooks, date-utils, api-client)
```
State management: React Query (Server state) + Context/Zustand برای UI state سبک — بدون نیاز به Redux کامل در MVP. فرم‌ها با `react-hook-form` + Zod validation. Date-picker شمسی به‌عنوان کامپوننت مشترک در `/shared`.

## 28. AI Coding Agent Requirements

Agent باید قبل از هر کار موارد زیر را بداند (منبع: فایل‌های Repository، بخش بعد):

- **معماری:** Modular Monolith، Clean Architecture layers، مرز بین Domain/Application/Infrastructure/API را نباید بشکند (مثلاً Domain نباید به EF Core وابسته شود).
- **Domain model دقیق** طبق بخش ۷ (اسم Entityها، فیلدها، Enumها ثابت‌اند مگر صراحتاً تغییر خواسته شود).
- **Convention نام‌گذاری:** انگلیسی برای کد/دیتابیس، فارسی فقط در Resource fileها/UI text.
- **قوانین تاریخ:** همیشه ذخیره UTC/Gregorian در DB، تبدیل شمسی فقط در Presentation.
- **قوانین پول:** ذخیره به `Rial (bigint)`، هرگز `float/double` برای پول.
- **Authorization:** هر Query باید `UserId` را فیلتر کند؛ Agent هرگز نباید Endpoint بدون Auth بسازد مگر `auth/*`.
- **Error handling:** فرمت خطای استاندارد (بخش ۲۶)، بدون Exception خام به کاربر.
- **Testing:** هیچ Feature بدون حداقل یک Unit test و یک Integration test مرتبط Merge نمی‌شود.
- **بدون AI dependency:** Agent هرگز نباید Feature‌ای بسازد که به یک LLM API خارجی وابسته باشد، مگر صراحتاً در تسک خواسته شود.
- **عدم اختراع Feature:** Agent فقط چیزی را که در Requirement/Issue آمده پیاده می‌کند؛ هر پیشنهاد اضافه باید در قالب "Suggestion" جدا مطرح شود، نه اجرا.

## 29. Repository Structure

```
/personal-obligation-manager
├── /src
│   ├── /Domain                 (POM.Domain)
│   ├── /Application             (POM.Application)
│   ├── /Infrastructure           (POM.Infrastructure)
│   ├── /Api                      (POM.Api)
│   └── /Web                      (frontend – React/Angular app)
├── /tests
│   ├── /UnitTests
│   └── /IntegrationTests
├── /docs
│   ├── ARCHITECTURE.md
│   ├── DOMAIN.md
│   ├── API.md
│   ├── DATABASE.md
│   ├── SECURITY.md
│   └── TESTING.md
├── AGENTS.md
├── README.md
├── CONTRIBUTING.md
├── docker-compose.yml
└── .github/workflows/ci.yml
```

### محتوای هر فایل مستندات

- **README.md** — معرفی محصول، نحوه اجرا local (docker-compose up)، لینک به سایر داک‌ها.
- **ARCHITECTURE.md** — دیاگرام لایه‌ها، توجیه Modular Monolith، مرز ماژول‌ها، قواعد وابستگی بین لایه‌ها.
- **DOMAIN.md** — دقیقاً بخش ۷ این سند (Entityها، فیلدها، Enumها، رابطه‌ها) — منبع حقیقت (Source of Truth) برای مدل داده.
- **API.md** — لیست کامل Endpointها، فرمت Request/Response، کدهای خطا.
- **DATABASE.md** — Schema کامل (بخش ۲۵)، Migration policy، Naming convention جداول/ستون‌ها.
- **SECURITY.md** — Threat model (بخش ۲۲)، Runbook بازیابی از Backup، سیاست مدیریت رمز عبور/Secrets.
- **TESTING.md** — استراتژی تست (بخش ۳۲)، نحوه اجرای تست‌ها، Coverage هدف.
- **CONTRIBUTING.md** — Git workflow، Commit convention (Conventional Commits پیشنهاد می‌شود: `feat:`, `fix:`, `chore:`...)، فرآیند Code review.
- **AGENTS.md** — بخش بعد.

## 30. AGENTS.md Requirements

فایل `AGENTS.md` باید شامل این بخش‌ها باشد (محتوای مرجع مستقیم برای AI Coding Agent، خلاصه و Actionable — نه تکرار کل این سند):

```markdown
# AGENTS.md

## پیش از هر تغییر
1. ARCHITECTURE.md و DOMAIN.md را بخوان.
2. مطمئن شو تغییر پیشنهادی مرز لایه‌ها را نمی‌شکند.
3. اگر تغییر بزرگ است (>3 فایل یا تغییر Schema)، اول یک Plan کوتاه بنویس و منتظر تأیید بمان.

## قوانین ثابت
- تاریخ: همیشه UTC/Gregorian در DB. تبدیل شمسی فقط در لایه Presentation.
- پول: bigint بر حسب ریال. هرگز float.
- Auth: هر Query سطح Application با UserId فیلتر شود.
- بدون وابستگی به AI/LLM API در منطق دامنه.
- بدون Feature جدید که در Issue/Task ذکر نشده.

## Conventions
- نام‌گذاری کد: انگلیسی، PascalCase برای کلاس، camelCase برای متغیر.
- هر Endpoint جدید باید DTO مجزا از Entity داشته باشد (بدون Expose مستقیم Entity).
- هر Feature جدید: حداقل 1 Unit Test + 1 Integration Test.

## Definition of Done
- [ ] کد Build می‌شود بدون Warning جدید
- [ ] تست‌ها سبز هستند (Unit + Integration مرتبط)
- [ ] Migration (در صورت نیاز) اضافه و تست شده
- [ ] DOMAIN.md / API.md در صورت تغییر Schema/Endpoint به‌روزرسانی شده
- [ ] بدون console.log / کد کامنت‌شده اضافه باقی‌مانده
- [ ] Authorization چک شده (UserId ownership)
```

## 31. Development Workflow for AI Agent

```
1. Analyze requirement         → خواندن Issue/Task
2. Inspect repository          → خواندن ARCHITECTURE.md, DOMAIN.md, فایل‌های مرتبط
3. Identify affected modules   → کدام لایه‌ها/فایل‌ها تحت تأثیرند
4. Create implementation plan  → لیست فایل‌های تغییر/اضافه، خلاصه تغییر Schema اگر هست
5. Ask clarification only if requirement is ambiguous or conflicting
6. Implement                   → طبق Convention‌های AGENTS.md
7. Write tests                 → Unit + Integration
8. Run tests                   → همه تست‌ها (نه فقط جدید) اجرا و سبز شوند
9. Review code                 → self-review نسبت به Definition of Done
10. Check security              → Authorization, Input validation, no secret leakage
11. Update documentation        → DOMAIN.md/API.md/DATABASE.md در صورت تغییر
12. Summarize changes           → خلاصه برای Commit/PR description
```

قاعده صریح: **Agent هرگز بدون بررسی ARCHITECTURE.md/DOMAIN.md اقدام به تغییر گسترده نمی‌کند، و هرگز Feature‌ای را خودش اختراع نمی‌کند مگر صراحتاً درخواست شده باشد. برای تغییرات بزرگ (Schema، بیش از ۳ فایل، افزودن Entity جدید)، Agent باید ابتدا Plan را ارائه دهد و منتظر تأیید بماند.**

## 32. Testing Strategy

- **Unit Tests:** روی Domain logic (مثلاً محاسبه Occurrence بعدی در RecurrenceRule) و Application Services — بدون دیتابیس واقعی (Mock کردن Interfaceها).
- **Integration Tests:** روی API Endpointها با دیتابیس تست واقعی (Postgres در Docker/Testcontainers) — شامل تست صریح **Data Isolation بین کاربران** (کاربر A نباید بتواند Obligation کاربر B را ببیند/تغییر دهد).
- **Recurrence Engine:** پوشش تست بالا و دقیق، چون قلب محصول است — تست همه پترن‌ها (روزانه، ماهانه با آخرین روز، سالگرد شمسی، هر X ماه) با Case‌های مرزی (اسفند کبیسه، ۳۱ فروردین → ۳۰ ماه بعد و…).
- **Reminder Engine:** تست idempotency (اجرای دوباره Job نباید پیام تکراری بفرستد).
- Coverage هدف MVP: ~۷۰٪ روی Domain/Application (نه عدد دلبخواه روی کل کد؛ کیفیت روی منطق حیاتی مهم‌تر از درصد کلی است).
- Frontend: تست‌های حداقلی روی فرم‌های اصلی (Create Obligation) با React Testing Library / Angular Testing utilities — عمیق‌تر شدن E2E (Playwright) به Should-Have موکول می‌شود.

## 33. Deployment Strategy

با توجه به ریسک دسترسی به سرویس‌های ابری بین‌المللی از ایران:

- **MVP:** Docker Compose تک‌سرور (API + Postgres + Frontend build شده پشت Nginx) — قابل استقرار روی هر VPS، چه اروپا/ترکیه (برای کاربران با VPN/دسترسی بهتر) چه یک ارائه‌دهنده داخلی مثل ArvanCloud (برای Latency و Reliability بهتر برای کاربر ایرانی).
- CI: GitHub Actions برای Build+Test+Docker image build.
- CD: در MVP دستی (SSH deploy یا اسکریپت ساده) کافی است؛ خودکارسازی کامل (ArgoCD و…) Overengineering برای این مرحله است.
- Backup: Cron روزانه `pg_dump` + آپلود به Storage جدا از سرور اصلی.

## 34. MVP Roadmap (پیشنهادی)

**فاز صفر (۱ هفته) — Foundation:** Repository setup، مستندات پایه، Auth، Domain model پایه، CI.

**فاز یک (۲-۳ هفته) — Core Obligation:** CRUD کامل Obligation (بدون Recurrence)، Category/Tag، Dashboard ساده، Persian/Gregorian date handling، List/Filter/Search.

**فاز دو (۲ هفته) — Recurrence + Reminder:** RecurrenceRule + Occurrence engine، Reminder + Email notification + Background job.

**فاز سه (۱-۲ هفته) — Attachments + Polish:** Attachment upload/download، Soft delete/Archive، Export، Account deletion، Activity log.

**نتیجه:** یک MVP قابل عرضه در حدود **۶–۸ هفته** برای یک Developer با کمک AI Agent، **بدون** Calendar View گرافیکی، Person/Asset، Web Push (این‌ها فاز بعدی بلافاصله بعد از MVP هستند، نه دور).

> پیشنهاد صریح من: اگر می‌خواهید MVP واقعاً کوچک‌ترین نسخه قابل ارزش باشد، **Person و Asset را هم به همین فاز چهار (بلافاصله بعد از MVP) منتقل کنید** — چون بدون آن‌ها هم محصول کاملاً کاربردی است (کاربر می‌تواند از Tag/Notes استفاده کند تا این دو ساخته شوند).

## 35. Future Roadmap

Calendar View کامل، Web Push، Family Sharing (چند کاربر روی یک Household)، PWA installable، گزارش‌های مالی ساده (نه Accounting)، Import از Google Calendar/Todoist، سپس — و فقط در صورت اثبات ارزش — لایه AI اختیاری (مثل Parsing هوشمندتر Quick Add یا پیشنهاد دسته‌بندی) به‌عنوان Add-on، نه وابستگی.

## 36. Risks

| ریسک | تأثیر | راهکار |
|---|---|---|
| دسترسی محدود/فیلترینگ سرویس‌های ابری خارجی (Email, Storage, Hosting) | بالا | Provider-agnostic design از ابتدا (Interfaceها)، تست Deploy روی گزینه داخلی |
| پیچیدگی محاسبات تقویم شمسی (کبیسه، تبدیل) در Recurrence | متوسط-بالا | تست‌نویسی سنگین + استفاده از کتابخانه‌های اثبات‌شده به‌جای پیاده‌سازی دستی |
| Overengineering توسط AI Agent (ساخت Entity/Feature اضافه) | متوسط | AGENTS.md صریح + الزام Plan قبل از تغییر بزرگ |
| مبهم ماندن مدل درآمدی و اثر آن روی معماری Quota | پایین (فعلاً) | فیلد Quota ساده از ابتدا در نظر گرفته شده بدون قفل‌کردن تصمیم پلن |
| کشیده شدن Scope به سمت Todo List عمومی یا برعکس Accounting کامل | متوسط | مرزهای صریح بخش ۵ و ۱۹ |

## 37. Potential Product Differentiators

- تقویم شمسی و تومان **Native**، نه Plugin — اکثر رقبای بین‌المللی این را ندارند.
- اتصال Obligation به Asset/Person → تاریخچه معنادار («تاریخچه کامل خودرو من») که در Todo Appها وجود ندارد.
- Recurrence engine واقعاً منعطف (سالگرد شمسی، آخرین روز ماه) که در ابزارهای عمومی معمولاً یا وجود ندارد یا محدود است.
- طراحی صریح "بدون AI به‌عنوان اسکلت" → قابل اعتماد و قابل پیش‌بینی برای موضوعی حساس مثل تعهدات مالی/حقوقی، با امکان افزودن AI بعداً به‌عنوان لایه کمکی نه هسته.

---
**پایان سند.** این فایل می‌تواند مستقیماً پایه `/docs/DOMAIN.md`، `/docs/ARCHITECTURE.md` و `AGENTS.md` قرار گیرد؛ توصیه می‌شود قبل از شروع کدنویسی، بخش ۰ (سؤالات Critical) را با تصمیم قطعی خودتان جایگزین کنید.
