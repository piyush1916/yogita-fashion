import React, { useEffect, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import useDebounce from "../hooks/useDebounce";
import productsService from "../services/productsService";
import categories from "../data/categories.mock.json";
import { SORT_OPTIONS, PRODUCT_PAGE_SIZE } from "../utils/constants";
import SearchBar from "../components/filters/SearchBar";
import FilterPanel from "../components/filters/FilterPanel";
import SortDropdown from "../components/filters/SortDropdown";
import ProductGrid from "../components/product/ProductGrid";
import Loader from "../components/common/Loader";

const Shop = () => {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const urlSearch = String(searchParams.get("q") || "").trim();
  const urlSort = String(searchParams.get("sort") || "").trim();
  const isLegacyOfferQuery = String(searchParams.get("tag") || "").toLowerCase() === "offers";

  const [search, setSearch] = useState("");
  const debouncedSearch = useDebounce(search);
  const [filters, setFilters] = useState({});
  const [sort, setSort] = useState("");
  const [page, setPage] = useState(1);
  const [products, setProducts] = useState([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (isLegacyOfferQuery) {
      navigate("/offers", { replace: true });
      return;
    }
    setSearch(urlSearch);
    setSort(SORT_OPTIONS.some((option) => option.value === urlSort) ? urlSort : "");
    setPage(1);
  }, [urlSearch, urlSort, isLegacyOfferQuery, navigate]);

  const load = async () => {
    setLoading(true);
    const res = await productsService.list({
      page,
      size: PRODUCT_PAGE_SIZE,
      search: debouncedSearch,
      filters,
      sort,
    });
    setProducts(res.items);
    setTotal(res.total);
    setLoading(false);
  };

  useEffect(() => {
    load();
  }, [page, debouncedSearch, filters, sort]);

  const pagesCount = Math.ceil(total / PRODUCT_PAGE_SIZE);

  return (
    <div className="container mx-auto px-4 py-10">
      <div className="mb-4">
        <SearchBar value={search} onChange={setSearch} />
      </div>
      <div className="flex flex-col gap-6 lg:flex-row">
        <div className="min-w-0 lg:w-1/4">
          <FilterPanel categories={categories} filters={filters} onChange={setFilters} />
        </div>
        <div className="min-w-0 lg:w-3/4">
          <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-end">
            <SortDropdown value={sort} onChange={setSort} options={SORT_OPTIONS} />
          </div>
          {loading ? (
            <Loader />
          ) : products.length === 0 ? (
            <div className="ordersPlaceholder">No products found for selected filters.</div>
          ) : (
            <>
              <ProductGrid products={products} />
              {pagesCount > 1 && (
                <div className="mt-4 flex justify-center gap-2">
                  {Array.from({ length: pagesCount }, (_, i) => (
                    <button
                      key={i}
                      className={`rounded border px-3 py-1 ${page === i + 1 ? "bg-indigo-600 text-white" : ""}`}
                      onClick={() => setPage(i + 1)}
                    >
                      {i + 1}
                    </button>
                  ))}
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
};

export default Shop;
