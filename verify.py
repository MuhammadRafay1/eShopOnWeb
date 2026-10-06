import json, ssl, time, urllib.request, urllib.error, math

BASE = "https://localhost:36823"
CTX = ssl.create_default_context()
CTX.check_hostname = False
CTX.verify_mode = ssl.CERT_NONE


def call(method, path, token=None, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, context=CTX, timeout=90) as r:
            raw = r.read().decode()
            return r.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try:
            return e.code, json.loads(raw)
        except Exception:
            return e.code, raw


def auth():
    s, b = call("POST", "/api/authenticate",
                body={"username": "demouser@microsoft.com", "password": "Pass@word1"})
    assert s == 200 and b.get("token"), (s, b)
    return b["token"]


def main():
    token = auth()
    print("AUTH ok")

    investor = {
        "firstName": "Ada", "lastName": "Lovelace", "email": "ada.test@example.com",
        "birthDate": "1985-12-10", "nationality": "DE",
        "address": {"line1": "10 Analytical Engine Way", "postcode": "10115", "city": "Berlin", "country": "DE"},
        "phoneNumber": "491700000000", "taxId": "26954371827", "taxCountry": "DE",
    }
    s, b = call("POST", "/api/investing/enrolment", token, investor)
    print("ENROL", s, b)
    assert s == 200, "enrolment failed"

    status = b.get("status")
    for i in range(30):
        s, b = call("GET", "/api/investing/enrolment", token)
        status = b.get("status") if isinstance(b, dict) else None
        print(f"  enrolment poll[{i}] {s} {b}")
        if status in ("active", "rejected"):
            break
        time.sleep(2)
    assert status == "active", f"enrolment did not become active (got {status})"
    print("ENROLMENT ACTIVE")

    # Discover a catalog item with the largest single-unit round-up.
    s, items = call("GET", "/api/catalog-items", token)
    assert s == 200, (s, items)
    lst = items["catalogItems"] if isinstance(items, dict) and "catalogItems" in items else items
    best = None
    for it in lst:
        price = float(it["price"])
        ru = round(math.ceil(price) - price, 2)
        if ru > 0 and (best is None or ru > best[1]):
            best = (it["id"], ru, price)
    assert best, "no catalog item with a fractional price"
    item_id, ru, price = best
    print(f"Using catalog item {item_id} price={price} round-up/order={ru}")

    n = int(math.ceil(10.0 / ru)) + 1
    print(f"Placing up to {n} orders to cross EUR 10...")
    total_set_aside = 0.0
    for i in range(n):
        s, b = call("POST", "/api/orders", token, {"items": [{"catalogItemId": item_id, "quantity": 1}]})
        assert s == 200, (s, b)
        total_set_aside += float(b["roundUpAmount"])
        s2, bal = call("GET", "/api/investing/balance", token)
        invested = float(bal["investedAmount"])
        print(f"  order[{i}] orderId={b['orderId']} roundUp={b['roundUpAmount']} "
              f"pending={bal['pendingAmount']} invested={bal['investedAmount']}")
        if invested > 0:
            print("INVESTMENT TRIGGERED")
            break

    # Reconcile investment status to terminal (settled/failed).
    final = None
    for i in range(20):
        s, inv = call("GET", "/api/investing/investments", token)
        s2, bal = call("GET", "/api/investing/balance", token)
        invs = inv["investments"] if isinstance(inv, dict) else inv
        print(f"  settle poll[{i}] investments={invs} balance={bal}")
        if invs and all(x["status"] in ("settled", "failed") for x in invs):
            final = invs
            break
        time.sleep(2)

    print("FINAL investments:", final)
    s, bal = call("GET", "/api/investing/balance", token)
    print("FINAL balance:", bal)


if __name__ == "__main__":
    main()
