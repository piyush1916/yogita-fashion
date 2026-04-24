const { chromium } = require("playwright");

const STORE_BASE = "http://127.0.0.1:5173";
const ADMIN_BASE = "http://127.0.0.1:5174";
const API_BASE = "http://127.0.0.1:5037";
const EDGE_PATH = "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe";

const stamp = `${Date.now()}`;
const testData = {
  userName: "QA User",
  userEmail: `qa.e2e.${stamp}@example.com`,
  userPassword: "QaPass@123",
  updatedName: "QA User Updated",
  updatedEmail: `qa.e2e.updated.${stamp}@example.com`,
  updatedPhone: "9876543210",
  updatedCity: "Pune",
  address: {
    fullName: `QA Address ${stamp}`,
    phone: "9876543210",
    line1: `Flat ${stamp.slice(-4)}, Test Street`,
    line2: "Near QA Circle",
    city: "Mumbai",
    state: "Maharashtra",
    pincode: "400001",
    landmark: "QA Tower",
  },
  couponCode: `QA${stamp.slice(-6)}`,
  productName: `QA E2E Product ${stamp}`,
  productUpdatedName: `QA E2E Product Updated ${stamp}`,
};

function compactText(value) {
  return String(value ?? "").replace(/\s+/g, " ").trim();
}

function numberFromCurrency(text) {
  const cleaned = String(text ?? "").replace(/[^0-9.]/g, "");
  const value = Number(cleaned);
  return Number.isFinite(value) ? value : 0;
}

async function waitForToast(page, expectedTextPart, timeout = 10000) {
  const locator = page.locator('[role="status"]').filter({ hasText: expectedTextPart }).last();
  await locator.waitFor({ state: "visible", timeout });
  return compactText(await locator.textContent());
}

async function dismissDialogs(page, answers) {
  page.on("dialog", async (dialog) => {
    const message = compactText(dialog.message());
    if (dialog.type() === "confirm") {
      await dialog.accept();
      return;
    }

    if (dialog.type() === "prompt") {
      let answer = "";
      for (const item of answers) {
        if (message.includes(item.match)) {
          answer = item.answer;
          break;
        }
      }
      await dialog.accept(answer);
      return;
    }

    await dialog.accept();
  });
}

async function attachDiagnostics(page, bucket) {
  page.on("console", (msg) => {
    if (msg.type() === "error") {
      bucket.consoleErrors.push(compactText(msg.text()));
    }
  });
  page.on("pageerror", (error) => {
    bucket.pageErrors.push(compactText(error.message));
  });
  page.on("response", async (response) => {
    const status = response.status();
    const url = response.url();
    if (status >= 500 && url.startsWith(API_BASE)) {
      let body = "";
      try {
        body = compactText(await response.text());
      } catch {
        body = "";
      }
      bucket.serverErrors.push({ url, status, body });
    }
  });
}

async function expectNoHorizontalScroll(page, name, results) {
  await page.waitForLoadState("networkidle");
  await page.waitForTimeout(500);
  const metrics = await page.evaluate(() => {
    const before = window.scrollX;
    window.scrollTo(999999, 0);
    const after = window.scrollX;
    window.scrollTo(before, 0);
    const doc = document.documentElement;
    const scroller = document.scrollingElement || doc;
    return {
      before,
      after,
      scrollWidth: scroller.scrollWidth,
      clientWidth: scroller.clientWidth,
    };
  });
  results.push({
    page: name,
    hasHorizontalScroll: metrics.after > 0 || metrics.scrollWidth > metrics.clientWidth + 1,
    metrics,
  });
}

async function setLocalStorageState(page, origin, entries) {
  await page.goto(origin, { waitUntil: "domcontentloaded" });
  await page.evaluate((items) => {
    localStorage.clear();
    for (const [key, value] of Object.entries(items)) {
      localStorage.setItem(key, value);
    }
  }, entries);
}

async function main() {
  const diagnostics = {
    store: { consoleErrors: [], pageErrors: [], serverErrors: [] },
    admin: { consoleErrors: [], pageErrors: [], serverErrors: [] },
  };

  const results = {
    user: {},
    admin: {},
    system: {},
    ui: {
      storeMobile: [],
      adminMobile: [],
    },
    issues: [],
  };

  const browser = await chromium.launch({
    headless: true,
    executablePath: EDGE_PATH,
  });

  try {
    const unauthorizedContext = await browser.newContext({ baseURL: ADMIN_BASE, viewport: { width: 1440, height: 900 } });
    const unauthorizedPage = await unauthorizedContext.newPage();
    await unauthorizedPage.goto(`${ADMIN_BASE}/dashboard`, { waitUntil: "networkidle" });
    results.system.adminRedirectWithoutLogin = /\/login(?:$|\?)/.test(unauthorizedPage.url());
    await unauthorizedContext.close();

    const adminContext = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const adminPage = await adminContext.newPage();
    await attachDiagnostics(adminPage, diagnostics.admin);
    await dismissDialogs(adminPage, [
      { match: "Add note for", answer: "QA status update" },
    ]);

    await adminPage.goto(`${ADMIN_BASE}/login`, { waitUntil: "networkidle" });
    await adminPage.getByLabel("Email").fill("admin@yogita.com");
    await adminPage.getByLabel("Password").fill("Khushi@1916");
    await adminPage.getByRole("button", { name: "Login to Admin Panel" }).click();
    await adminPage.waitForURL("**/dashboard", { timeout: 15000 });
    results.admin.login = true;

    const adminSession = await adminPage.evaluate(() => ({
      token: localStorage.getItem("yf_admin_token") || "",
      session: localStorage.getItem("yf_admin_session") || "",
    }));

    await adminPage.getByRole("link", { name: "Coupons" }).click();
    await adminPage.waitForURL("**/coupons");
    await adminPage.getByLabel("Coupon Code *").fill(testData.couponCode);
    await adminPage.getByLabel("Discount % *").fill("10");
    await adminPage.getByLabel("Min Order Amount").fill("500");
    await adminPage.getByLabel("Max Uses (Global)").fill("5");
    await adminPage.getByLabel("Max Uses Per User").fill("1");
    await adminPage.getByRole("button", { name: "Create Coupon" }).click();
    await adminPage.locator("table").waitFor();
    await adminPage.locator("tbody tr").filter({ hasText: testData.couponCode }).first().waitFor({ timeout: 10000 });
    results.admin.couponCreated = true;

    const couponRow = adminPage.locator("tbody tr").filter({ hasText: testData.couponCode }).first();
    await couponRow.getByRole("button", { name: "Edit" }).click();
    await adminPage.getByLabel("Discount % *").fill("12");
    await adminPage.getByRole("button", { name: "Update Coupon" }).click();
    await couponRow.waitFor({ timeout: 10000 });
    results.admin.couponUpdated = await couponRow.textContent().then((t) => compactText(t).includes("12%"));

    await adminPage.getByRole("link", { name: "Products" }).click();
    await adminPage.waitForURL("**/products");
    await adminPage.locator("section").getByRole("link", { name: "Add Product" }).click();
    await adminPage.waitForURL("**/products/new");
    await adminPage.getByLabel("Product Name *").fill(testData.productName);
    await adminPage.getByLabel("Category *").fill("QA Category");
    await adminPage.getByLabel("Description *").fill(`Description for ${testData.productName}`);
    await adminPage.getByLabel("Price (INR) *").fill("1234");
    await adminPage.getByLabel("Original Price (INR)").fill("1500");
    await adminPage.getByLabel("Size *").fill("S, M");
    await adminPage.getByLabel("Color *").fill("Green, Gold");
    await adminPage.getByLabel("Stock *").fill("8");
    await adminPage.getByLabel("Brand *").fill("QA Brand");
    await adminPage.getByLabel("Image URL *").fill("https://via.placeholder.com/600x600.png?text=QA+E2E");
    await adminPage.getByRole("button", { name: "Save Product" }).click();
    await adminPage.waitForURL("**/products");
    await adminPage.getByPlaceholder("Search by name, category, brand...").fill(testData.productName);
    const createdProductRow = adminPage.locator("tbody tr").filter({ hasText: testData.productName }).first();
    await createdProductRow.waitFor({ timeout: 10000 });
    results.admin.productCreated = true;
    const editHref = await createdProductRow.getByRole("link", { name: "Edit" }).getAttribute("href");
    results.admin.createdProductId = String(editHref || "").split("/")[2] || "";

    await createdProductRow.getByRole("link", { name: "Edit" }).click();
    await adminPage.waitForURL(/\/products\/\d+\/edit$/);
    await adminPage.getByLabel("Product Name *").fill(testData.productUpdatedName);
    await adminPage.getByLabel("Price (INR) *").fill("1333");
    await adminPage.getByLabel("Stock *").fill("9");
    await adminPage.getByRole("button", { name: "Update Product" }).click();
    await adminPage.waitForURL("**/products");
    await adminPage.getByPlaceholder("Search by name, category, brand...").fill(testData.productUpdatedName);
    const updatedProductRow = adminPage.locator("tbody tr").filter({ hasText: testData.productUpdatedName }).first();
    await updatedProductRow.waitFor({ timeout: 10000 });
    results.admin.productUpdated = true;

    const storeContext = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const storePage = await storeContext.newPage();
    await attachDiagnostics(storePage, diagnostics.store);
    await dismissDialogs(storePage, [
      { match: "Return reason", answer: "Size issue" },
    ]);

    await storePage.goto(`${STORE_BASE}/login`, { waitUntil: "networkidle" });
    await storePage.getByLabel("Email").fill(testData.userEmail);
    await storePage.getByLabel("Password").fill("WrongPass@123");
    await storePage.getByRole("button", { name: /^Login$/ }).click();
    results.user.invalidLoginMessage = await waitForToast(storePage, "Invalid email or password.");

    await storePage.goto(`${STORE_BASE}/register`, { waitUntil: "networkidle" });
    await storePage.getByLabel("Full Name").fill(testData.userName);
    await storePage.getByLabel("Email").fill(testData.userEmail);
    await storePage.getByLabel("Password").fill(testData.userPassword);
    await storePage.getByRole("button", { name: "Create Account" }).click();
    await storePage.waitForURL(`${STORE_BASE}/`, { timeout: 15000 });
    results.user.signup = true;

    await storePage.goto(`${STORE_BASE}/profile`, { waitUntil: "networkidle" });
    const joinText = compactText(await storePage.locator(".profileMetaRow").filter({ hasText: "Joined" }).locator("strong").textContent());
    results.user.joinDate = joinText;
    results.user.joinDateIsReal = !joinText.includes("1970") && joinText !== "N/A";

    await storePage.getByRole("button", { name: "Logout" }).click();
    await storePage.waitForURL(`${STORE_BASE}/`, { timeout: 10000 });
    results.user.logoutAfterSignup = true;

    await storePage.goto(`${STORE_BASE}/login`, { waitUntil: "networkidle" });
    await storePage.getByLabel("Email").fill(testData.userEmail);
    await storePage.getByLabel("Password").fill(testData.userPassword);
    await storePage.getByRole("button", { name: /^Login$/ }).click();
    await storePage.waitForURL(`${STORE_BASE}/`, { timeout: 15000 });
    results.user.validLogin = true;

    await storePage.goto(`${STORE_BASE}/shop?q=Anarkali`, { waitUntil: "networkidle" });
    const wishlistCard = storePage.locator("a[href^='/product/']").filter({ hasText: "Anarkali Suit Sets" }).first();
    await wishlistCard.waitFor({ timeout: 10000 });
    const productCard = storePage.locator("div.relative.border.rounded").filter({ hasText: "Anarkali Suit Sets" }).first();
    await productCard.getByRole("button", { name: "Add to wishlist" }).click();
    await storePage.waitForTimeout(1000);
    await storePage.goto(`${STORE_BASE}/wishlist`, { waitUntil: "networkidle" });
    const wishlistFirstTitle = compactText(await storePage.locator(".ordersCardContent h2").first().textContent());
    results.user.wishlistProductTitle = wishlistFirstTitle;
    results.user.wishlistCorrectProduct = wishlistFirstTitle === "Anarkali Suit Sets";
    await storePage.getByRole("link", { name: "View Product" }).first().click();
    await storePage.waitForURL("**/product/**");
    results.user.viewProduct = compactText(await storePage.locator("h1").first().textContent());
    await storePage.getByRole("button", { name: "Remove from Wishlist" }).click();
    await waitForToast(storePage, "Removed from wishlist.");
    await storePage.getByRole("button", { name: "Add to Wishlist" }).click();
    await waitForToast(storePage, "Added to wishlist.");
    await storePage.getByRole("button", { name: "Add to Cart" }).click();
    await storePage.waitForURL("**/cart", { timeout: 15000 });

    results.user.cartInitialTitle = compactText(await storePage.locator(".space-y-4 h3.font-semibold").first().textContent());
    await storePage.locator("button", { hasText: "+" }).first().click();
    await storePage.waitForTimeout(500);
    results.user.cartQty = compactText(await storePage.locator("div.flex.items-center.space-x-2.mt-2 span").first().textContent());

    await storePage.getByPlaceholder("Coupon code").fill("INVALIDCODE");
    await storePage.getByRole("button", { name: "Apply" }).click();
    results.user.invalidCouponMessage = await waitForToast(storePage, "Invalid");

    await storePage.getByPlaceholder("Coupon code").fill(testData.couponCode);
    await storePage.getByRole("button", { name: "Apply" }).click();
    results.user.validCouponMessage = await waitForToast(storePage, "Coupon applied");
    const subtotalText = compactText(await storePage.locator("h3.font-semibold").filter({ hasText: "Subtotal:" }).textContent());
    const totalWithCouponText = compactText(await storePage.locator("p.mt-1.font-semibold").textContent());
    const discountText = compactText(await storePage.locator("p.text-green-600").textContent());
    results.user.cartSubtotal = numberFromCurrency(subtotalText);
    results.user.cartDiscount = numberFromCurrency(discountText);
    results.user.cartTotalWithCoupon = numberFromCurrency(totalWithCouponText);

    await storePage.getByRole("button", { name: "Remove Coupon" }).click();
    results.user.removeCouponMessage = await waitForToast(storePage, "Coupon removed");
    await storePage.getByPlaceholder("Coupon code").fill(testData.couponCode);
    await storePage.getByRole("button", { name: "Apply" }).click();
    await waitForToast(storePage, "Coupon applied");
    results.user.validCouponReapplied = true;

    await storePage.getByRole("button", { name: "Continue to Checkout" }).click();
    await storePage.waitForURL("**/checkout");
    await storePage.locator('input[name="name"]').fill(testData.updatedName);
    await storePage.locator('input[name="phone"]').fill(testData.updatedPhone);
    await storePage.locator('input[name="email"]').fill(testData.updatedEmail);
    await storePage.locator('input[name="address"]').fill(`${testData.address.line1}, ${testData.address.line2}`);
    await storePage.locator('input[name="city"]').fill(testData.updatedCity);
    await storePage.locator('input[name="pincode"]').fill(testData.address.pincode);
    await storePage.locator('select[name="payment"]').selectOption("ONLINE");
    await storePage.getByRole("button", { name: "Place Order" }).click();
    await storePage.waitForURL("**/track-order", { timeout: 15000 });
    const orderMetaValues = await storePage.locator(".ordersCardMeta strong").allTextContents();
    results.user.orderNumber = compactText(orderMetaValues[0] || "");
    results.user.trackingNumber = compactText(orderMetaValues[1] || "");
    results.user.orderStatusAfterCreate = compactText(orderMetaValues[2] || "");
    results.user.orderTotal = numberFromCurrency(orderMetaValues[3] || "");
    results.user.orderPlaced = true;

    await storePage.goto(`${STORE_BASE}/orders`, { waitUntil: "networkidle" });
    const orderCard = storePage.locator(".ordersCard").filter({ hasText: results.user.orderNumber }).first();
    await orderCard.waitFor({ timeout: 15000 });
    results.user.orderHistoryVisible = true;

    await storePage.goto(`${STORE_BASE}/track-order`, { waitUntil: "networkidle" });
    await storePage.getByLabel("Order ID").fill("INVALID-ORDER");
    await storePage.getByLabel("Phone or Email").fill(testData.updatedEmail);
    await storePage.getByRole("button", { name: "Track Order" }).click();
    results.user.invalidTrackMessage = await waitForToast(storePage, "Order not found");

    await storePage.getByLabel("Order ID").fill(results.user.orderNumber);
    await storePage.getByLabel("Phone or Email").fill(testData.updatedEmail);
    await storePage.getByRole("button", { name: "Track Order" }).click();
    await storePage.locator(".ordersCardMeta").first().waitFor({ timeout: 15000 });
    results.user.validTrackStatus = compactText((await storePage.locator(".ordersCardMeta strong").allTextContents())[2] || "");

    await storePage.goto(`${STORE_BASE}/support`, { waitUntil: "networkidle" });
    await storePage.getByLabel("Name").fill(testData.updatedName);
    await storePage.getByLabel("Phone or Email").fill(testData.updatedEmail);
    await storePage.getByLabel("Order ID (optional)").fill(results.user.orderNumber);
    await storePage.getByLabel("Message").fill(`Need help with order ${results.user.orderNumber} for QA test.`);
    await storePage.getByRole("button", { name: "Submit Request" }).click();
    const supportCreateToast = await waitForToast(storePage, "Support ticket created:");
    results.user.supportCreateMessage = supportCreateToast;
    const supportIdMatch = supportCreateToast.match(/(\d+)/);
    results.user.supportId = supportIdMatch ? supportIdMatch[1] : "";
    await storePage.locator("article").filter({ hasText: results.user.supportId }).first().waitFor({ timeout: 10000 });
    results.user.supportVisible = true;

    await storePage.goto(`${STORE_BASE}/address`, { waitUntil: "networkidle" });
    await storePage.getByLabel("Full Name").fill(testData.address.fullName);
    await storePage.getByLabel("Phone").fill(testData.address.phone);
    await storePage.getByLabel("Address Line 1").fill(testData.address.line1);
    await storePage.getByLabel("Address Line 2").fill(testData.address.line2);
    await storePage.getByLabel("City").fill(testData.address.city);
    await storePage.getByLabel("State").fill(testData.address.state);
    await storePage.getByLabel("Pincode").fill(testData.address.pincode);
    await storePage.getByLabel("Landmark").fill(testData.address.landmark);
    await storePage.getByRole("button", { name: "Save Address" }).click();
    results.user.addressCreatedMessage = await waitForToast(storePage, "Address saved");
    const addressCard = storePage.locator("article").filter({ hasText: testData.address.fullName }).first();
    await addressCard.waitFor({ timeout: 10000 });
    results.user.addressAdded = true;
    await addressCard.getByRole("button", { name: "Edit" }).click();
    await storePage.getByLabel("City").fill("Pune");
    await storePage.getByRole("button", { name: "Update Address" }).click();
    results.user.addressUpdatedMessage = await waitForToast(storePage, "Address updated");
    await storePage.locator("article").filter({ hasText: "Pune" }).first().waitFor({ timeout: 10000 });
    results.user.addressUpdated = true;
    await storePage.locator("article").filter({ hasText: testData.address.fullName }).first().getByRole("button", { name: "Delete" }).click();
    results.user.addressDeletedMessage = await waitForToast(storePage, "Address removed");
    results.user.addressDeleted = !(await storePage.locator("article").filter({ hasText: testData.address.fullName }).count());

    await storePage.goto(`${STORE_BASE}/profile`, { waitUntil: "networkidle" });
    await storePage.getByLabel("Full Name").fill(testData.updatedName);
    await storePage.getByLabel("Email").fill(testData.updatedEmail);
    await storePage.getByLabel("Phone").fill(testData.updatedPhone);
    await storePage.getByLabel("City").fill(testData.updatedCity);
    await storePage.getByRole("button", { name: "Save Changes" }).click();
    results.user.profileUpdateMessage = await waitForToast(storePage, "Profile updated successfully");
    results.user.profileUpdated = true;
    results.user.changePasswordAvailable = (await storePage.getByText(/Change Password/i).count()) > 0
      || (await storePage.locator('input[type="password"]').count()) > 0;

    const storeSession = await storePage.evaluate(() => ({
      token: localStorage.getItem("yf_auth_token") || "",
      session: localStorage.getItem("yf_auth_session") || "",
    }));
    results.user.storeSession = storeSession;

    await adminPage.getByRole("link", { name: "Orders" }).click();
    await adminPage.waitForURL("**/orders");
    await adminPage.getByPlaceholder("Search by order id, customer, status, tracking...").fill(results.user.orderNumber);
    const orderRow = adminPage.locator("tbody tr").filter({ hasText: results.user.orderNumber }).first();
    await orderRow.waitFor({ timeout: 15000 });
    await orderRow.locator("select").selectOption("Delivered");
    await orderRow.getByRole("button", { name: "Update" }).click();
    await orderRow.waitFor({ timeout: 10000 });
    results.admin.orderStatusUpdated = compactText(await orderRow.textContent()).includes("Delivered");

    await storePage.goto(`${STORE_BASE}/orders`, { waitUntil: "networkidle" });
    const deliveredCard = storePage.locator(".ordersCard").filter({ hasText: results.user.orderNumber }).first();
    await deliveredCard.waitFor({ timeout: 15000 });
    results.user.orderStatusInStore = compactText(await deliveredCard.locator(".ordersCardMeta strong").nth(3).textContent());
    await deliveredCard.getByRole("button", { name: /Request Return/i }).click();
    results.user.returnRequestMessage = await waitForToast(storePage, "Return request submitted");
    await deliveredCard.locator(".ordersReturnChip").waitFor({ timeout: 10000 });
    results.user.returnRequested = compactText(await deliveredCard.locator(".ordersReturnChip").textContent());

    await adminPage.getByRole("link", { name: "Support" }).click();
    await adminPage.waitForURL("**/support-requests");
    await adminPage.getByPlaceholder("Search by subject, message, order, customer...").fill(results.user.supportId);
    const supportRow = adminPage.locator("tbody tr").filter({ hasText: results.user.supportId }).first();
    await supportRow.waitFor({ timeout: 15000 });
    await supportRow.locator("select").selectOption("Resolved");
    await supportRow.waitFor({ timeout: 500 });
    results.admin.supportStatusUpdated = compactText(await supportRow.textContent()).includes("Resolved");

    await storePage.goto(`${STORE_BASE}/support`, { waitUntil: "networkidle" });
    const supportCard = storePage.locator("article").filter({ hasText: results.user.supportId }).first();
    await supportCard.waitFor({ timeout: 10000 });
    results.user.supportStatusInStore = compactText(await supportCard.locator("span.rounded-full").textContent());

    await adminPage.getByRole("link", { name: "Coupons" }).click();
    await adminPage.waitForURL("**/coupons");
    const finalCouponRow = adminPage.locator("tbody tr").filter({ hasText: testData.couponCode }).first();
    await finalCouponRow.getByRole("button", { name: "Delete" }).click();
    results.admin.couponDeleted = (await adminPage.locator("tbody tr").filter({ hasText: testData.couponCode }).count()) === 0;

    await adminPage.getByRole("link", { name: "Products" }).click();
    await adminPage.waitForURL("**/products");
    await adminPage.getByPlaceholder("Search by name, category, brand...").fill(testData.productUpdatedName);
    const finalProductRow = adminPage.locator("tbody tr").filter({ hasText: testData.productUpdatedName }).first();
    await finalProductRow.getByRole("button", { name: "Delete" }).click();
    results.admin.productDeleted = (await adminPage.locator("tbody tr").filter({ hasText: testData.productUpdatedName }).count()) === 0;

    await storePage.goto(`${STORE_BASE}/shop?q=${encodeURIComponent(testData.productUpdatedName)}`, { waitUntil: "networkidle" });
    results.admin.productDeletedInStore = await storePage.getByText("No products found for selected filters.").count() > 0;

    await adminPage.goto(`${ADMIN_BASE}/audit-logs`, { waitUntil: "networkidle" });
    results.admin.auditLogsPageText = compactText(await adminPage.locator("body").textContent());

    // Mobile overflow checks
    await storePage.setViewportSize({ width: 390, height: 844 });
    await storePage.goto(`${STORE_BASE}/`, { waitUntil: "networkidle" });
    await expectNoHorizontalScroll(storePage, "Store Home", results.ui.storeMobile);
    await storePage.goto(`${STORE_BASE}/shop`, { waitUntil: "networkidle" });
    await expectNoHorizontalScroll(storePage, "Store Shop", results.ui.storeMobile);
    await storePage.goto(`${STORE_BASE}/product/1`, { waitUntil: "networkidle" });
    await expectNoHorizontalScroll(storePage, "Store Product", results.ui.storeMobile);
    await setLocalStorageState(storePage, STORE_BASE, {
      yf_auth_token: storeSession.token,
      yf_auth_session: storeSession.session,
    });
    await storePage.goto(`${STORE_BASE}/profile`, { waitUntil: "networkidle" });
    await expectNoHorizontalScroll(storePage, "Store Profile", results.ui.storeMobile);

    await adminPage.setViewportSize({ width: 390, height: 844 });
    await setLocalStorageState(adminPage, ADMIN_BASE, {
      yf_admin_token: adminSession.token,
      yf_admin_session: adminSession.session,
    });
    await adminPage.goto(`${ADMIN_BASE}/dashboard`, { waitUntil: "networkidle" });
    await expectNoHorizontalScroll(adminPage, "Admin Dashboard", results.ui.adminMobile);
    await adminPage.goto(`${ADMIN_BASE}/products`, { waitUntil: "networkidle" });
    await expectNoHorizontalScroll(adminPage, "Admin Products", results.ui.adminMobile);
    await adminPage.goto(`${ADMIN_BASE}/support-requests`, { waitUntil: "networkidle" });
    await expectNoHorizontalScroll(adminPage, "Admin Support", results.ui.adminMobile);

    // API authorization checks
    const apiContext = await browser.newContext();
    const apiPage = await apiContext.newPage();
    const unauthorizedStatus = await apiPage.evaluate(async (base) => {
      const response = await fetch(`${base}/audit-logs`);
      return response.status;
    }, API_BASE);
    const userForbiddenStatuses = await apiPage.evaluate(async ({ base, token, supportId }) => {
      const statuses = {};
      const supportResponse = await fetch(`${base}/support/requests/${supportId}/status`, {
        method: "PATCH",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({ status: "Closed" }),
      });
      statuses.support = supportResponse.status;
      const productResponse = await fetch(`${base}/products`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          name: "Forbidden Product",
          description: "Forbidden Product",
          category: "Forbidden",
          price: 100,
          originalPrice: 100,
          stock: 1,
          size: "S",
          color: "Black",
          brand: "QA",
          imageUrl: "https://via.placeholder.com/1",
        }),
      });
      statuses.product = productResponse.status;
      return statuses;
    }, { base: API_BASE, token: storeSession.token, supportId: results.user.supportId });
    await apiContext.close();

    results.system.unauthorizedAdminApiStatus = unauthorizedStatus;
    results.system.customerForbiddenSupportStatus = userForbiddenStatuses.support;
    results.system.customerForbiddenProductStatus = userForbiddenStatuses.product;

    results.system.serverErrors = [...diagnostics.store.serverErrors, ...diagnostics.admin.serverErrors];
    results.system.consoleErrors = [...diagnostics.store.consoleErrors, ...diagnostics.admin.consoleErrors];
    results.system.pageErrors = [...diagnostics.store.pageErrors, ...diagnostics.admin.pageErrors];
  } finally {
    await browser.close();
  }

  process.stdout.write(JSON.stringify(results, null, 2));
}

main().catch((error) => {
  console.error(JSON.stringify({ fatal: compactText(error.stack || error.message || String(error)) }, null, 2));
  process.exit(1);
});
