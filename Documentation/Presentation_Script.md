# 🎓 سكريبت العرض — Mall Loyalty System Backend

---

## 📌 مقدمة

> **"دكتور، هاذ المشروع هو Backend لنظام نقاط الولاء في المولات، مبني بـ ASP.NET Core 8 وقاعدة بيانات PostgreSQL على السحابة.**
> **هلق رح أعرض عليك ثلاث أجزاء: Unit Testing، Code Coverage، وPostman Automated Testing."**

---

## 🧪 الجزء الأول — Unit Testing (Visual Studio)

> *افتح Visual Studio → اضغط Test → Test Explorer → Run All*

---

### شو تحكي:

**"نبدأ بالـ Unit Testing.**

عندنا **165 تست** موزعين على مجلدين:**"**

| المجلد | المحتوى |
|---|---|
| `Tests/Services/` | تستات الـ business logic الأساسية |
| `Tests/CoverageGapTests/` | تستات إضافية للـ edge cases والـ error paths |

**"كل تست بيشتغل بـ xUnit مع EF Core InMemory Database —
يعني ما بحتاج database حقيقية.
كل تست بيبني بياناته الخاصة وبتنحذف تلقائياً بعد ما يخلص.**"

> *انتظر لحتى يخلص الـ Run*

**"شايف دكتور؟ — 165 تست، كلهم أخضر، صفر failures.**

هاذا بيثبت إن الـ business logic شغّال صح بكل الحالات المتوقعة والحالات الاستثنائية."**

---

## 📊 الجزء الثاني — Code Coverage Report

> *افتح المتصفح على ملف* `CoverageReport/index.html`

---

### شو تحكي:

**"هاذا الـ Code Coverage Report — اتولد تلقائياً بعد تشغيل التستات باستخدام Coverlet وReportGenerator.**

**الريبورت بيقيس كم نسبة من الكود اتغطى بتستات.**

شايف دكتور؟ وصلنا لـ **98.1% Line Coverage** — يعني **98%** من الكود اتاختبر.**"**

---

### شرح الألوان:

| اللون | المعنى |
|---|---|
| 🟢 أخضر | كود اتاختبر وعدا التست عليه |
| 🔴 أحمر | كود ما وصله التست |

---

**"الأحمر الموجود هو حالات استثنائية صعب تختبرها — مثل انقطاع الشبكة وأخطاء قاعدة البيانات.**

المعيار الصناعي الاحترافي هو **80%** —
نحنا وصلنا **98.1%** — يعني تجاوزنا المعيار بفرق كبير."**

---

## 📬 الجزء الثالث — Postman Automated Testing

> *افتح Postman → Collections → Mall Loyalty System → Runner → اختر البيئة → Run*

---

### شو تحكي:

**"الجزء الأخير هو الـ Automated Integration Testing باستخدام Postman.**

هاذا مختلف عن الـ Unit Tests —
هون بنبعث HTTP requests حقيقية للـ API وهي شغّالة فعلياً على السيرفر."**

---

### مجلدات الـ Collection:

| المجلد | شو بيختبر |
|---|---|
| 🔐 Auth | Register، Login، Logout، Manager Login |
| 🏪 Stores | إنشاء وتعديل وحذف المحلات |
| 📢 Announcements | إنشاء وتعديل وتثبيت وحذف الإعلانات |
| 🎁 Offers | إنشاء وإدارة العروض |
| 🧾 Transactions | معالجة الفواتير وكسب النقاط |
| 🎫 Coupons | عرض وشراء واستخدام الكوبونات |
| 📊 Dashboard | Analytics للمانجر |
| 🤖 Chatbot | أسئلة باللغتين العربية والإنجليزية |
| 🔒 Security Tests | اختبار الحماية من الوصول غير المصرح |

---

### نقاط مميزة:

**"في ميزات ذكية بالـ collection:"**

- ✅ **كل Run بيولد مستخدم جديد تلقائياً** — ما بيتعارض مع البيانات القديمة
- ✅ **كل Receipt عنده ID فريد** — يتجنب أخطاء الـ duplicate
- ✅ **بعد الـ Logout بيعمل re-login تلقائي** — عشان باقي التستات ما تفشل
- ✅ **الـ sessionId بتنتقل تلقائياً** من طلب لطلب عبر environment variables

> *انتظر لحتى يخلص الـ Runner*

**"شايف دكتور؟ — 86+ تست بيعدوا.**

هاذا بيثبت إن الـ API شغّالة end-to-end —
من أول ما المستخدم يسجل، لحتى يكسب نقاط، ويصرف كوبون."**

---

## ✅ الخلاصة

| | النتيجة |
|---|---|
| 🧪 Unit Tests | **165 تست — 0 failures** |
| 📊 Code Coverage | **98.1% line coverage** |
| 📬 Postman Tests | **86+ automated test — كلهم ناجحين** |

---

> **"النظام مختبر من كل الجهات —
> Unit Level للـ business logic،
> وIntegration Level للـ API الكاملة.**
>
> شكراً دكتور."**
