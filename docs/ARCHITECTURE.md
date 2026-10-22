# Architecture and engineering tradeoffs

| Component | Responsibility |
| --- | --- |
| MainWindow / ImportMappingWindow | Controls, dialogs, selection, and navigation |
| MainViewModel | Session, filters, dashboard collections, candidate, and scenario inputs |
| CsvImportService | Header inspection, profile selection, mapping, and normalization |
| AnalysisEngine | Segment rankings, host comparisons, market gaps, and recommendations |
| InvestmentAnalysisService | Loan calculations, cost scenarios, and comparables |
| KnowledgeBaseService | SQLite upserts and retrieval of reference listings |
| TuroListing and result models | Typed data shared between services and presentation |

## Data flow

The window inspects selected files and routes recognized exports to the known-header profile. Other files enter the mapping preview. Parsed rows become TuroListing objects. The viewmodel merges the import, persists listings, refreshes filters, and requests a dashboard snapshot. Selecting a candidate updates its details, comparables, and ROI projection.

SQLite stores listing attributes and serialized monthly history. Upserts use a listing fingerprint rather than appending every observation. This provides a reusable reference collection, not a full historical snapshot archive or a vector database.

## Ranking and tradeoffs

Segment scores combine normalized revenue, utilization, trips, favorites, reviews, and visible competition. Candidate recommendations additionally use reference groups and listing-level signals. Reasons are generated from explicit rules. Neither scores nor recommendation bands represent calibrated probabilities.

Services can run without launching the dashboard. WPF binding connects selection and scenario changes to presentation. The single project keeps deployment straightforward; the large viewmodel is a maintainability tradeoff as features grow.

Missing history can change the population contributing to monthly averages. Raw trip and review counts reflect listing age as well as demand. Host grouping and visible competition depend on export coverage.

## Verification boundaries

The smoke executable checks representative imports, dashboard output, and persistence using deterministic fixtures. It is not a comprehensive regression suite. UI behavior, large-file responsiveness, malformed-value coverage, and recommendation quality need additional evaluation.

## Next engineering steps

1. Detect incomplete known exports independently of validity, so missing critical headers cannot silently route to the generic importer.
2. Track revenue basis and currency explicitly to prevent duplicate fee deductions and aggregation across currencies.
3. Add regression cases for malformed numbers, missing months, duplicates, and user-cleared mappings.
4. Separate historical observations from current records before measuring temporal trends or training a model.
5. Define a future observed outcome and compare learned ranking with the current rules using time-based holdouts and leakage checks.
6. Measure import latency and move expensive work off the UI thread where needed.

These are planned improvements, not implemented capabilities.
