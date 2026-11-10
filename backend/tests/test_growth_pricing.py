"""Public crawl pricing config for landing + wizard."""


def test_crawl_pricing_defaults(client) -> None:
    res = client.get("/api/v1/growth/crawl-pricing")
    assert res.status_code == 200
    body = res.json()
    assert body["price_per_page_cents"] == 2
    assert body["free_pages_allowance"] == 5000
    assert body["billable_unit"] == "pages_with_results"
    assert body["anonymous_crawl_allowed"] is True
