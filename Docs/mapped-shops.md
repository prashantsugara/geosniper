# Shops from map data

Public OSM `shop` values are retained as `details.shop` and normalized into `details.category`. Unnamed active shop nodes are retained; `shop=no` and `shop=vacant` do not generate retail geometry. The detail whitelist preserves the shop value through the map service and sector cache. Shop values from supplemental map data enrich existing matched features.

Mapped shop building footprints and shop nodes associated with a nearby building receive street-facing display glazing, a door and handle, and a striped canopy. Existing facade signage includes supplied names and categories. Pharmacy and other recognized category colors are reused. Multiple tenants in one building share its sign and storefront.

A shop node without a suitable building can receive a small counter kiosk at its exact mapped coordinate. Kiosks are rejected when their footprint overlaps a mapped road, building, water, or steep ground. Shop geometry is combined by material. Up to 24 nearby storefront/kiosk details are built on mobile and 64 on desktop, within 240 metres of the sector detail focus; mapped signs remain managed by the signage pass.

Validation: `Tools/check_code.ps1` and isolated Unity `MappedShopChecks.Run` passed. Checks cover shop subtypes, unnamed nodes, inactive-shop filtering, cache decoding, shop-way storefronts, hosted shop-node storefronts, names/categories, exact kiosk coordinates and rejection of a kiosk on a road. Synthetic preview: `output/mapped-shops/preview.png`.
