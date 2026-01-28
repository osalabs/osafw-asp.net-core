# Code Health Report
## 1) Executive Summary
- **Average Code Health:** 7.13
- **Hotspot Code Health:** 5.97
- **Worst Score:** 2.47 (osafw-app/App_Code/models/Dev/CodeGen.cs)

**Top Refactoring Targets**
1. osafw-app/App_Code/fw/ParsePage.cs (priority 6.2451, health 2.70)
2. osafw-app/App_Code/models/Dev/CodeGen.cs (priority 5.9208, health 2.47)
3. osafw-app/App_Code/fw/FwDynamicController.cs (priority 5.6691, health 4.07)
4. osafw-app/App_Code/fw/FW.cs (priority 5.4711, health 4.02)
5. osafw-app/App_Code/fw/DB.cs (priority 5.3838, health 3.99)

## 2) Repository Overview
- Hotspot window: 2024-01-29 to 2026-01-28 (last 2 years)
- Tracked source files analyzed: 1164
- Total LoC (non-blank, non-comment when possible): 55055

**Languages by LoC**
| Extension | LoC |
|---|---|
| .cs | 27338 |
| .html | 11979 |
| .md | 4510 |
| .js | 3368 |
| .json | 3365 |
| .css | 1827 |
| .sql | 1347 |
| .sel | 863 |
| .txt | 224 |
| <none> | 78 |
| .csproj | 62 |
| .bat | 50 |
| .sln | 36 |
| .svg | 8 |

**Top 10 directories by LoC**
| Directory | LoC |
|---|---|
| osafw-app | 48940 |
| osafw-tests | 5360 |
| docs | 292 |
| README.md | 238 |
| .github | 82 |
| scripts | 65 |
| osafw-asp.net-core.sln | 36 |
| LICENSE | 17 |
| .editorconfig | 13 |
| .gitignore | 11 |

## 3) Code Health KPIs
- **Hotspot Code Health (LoC-weighted):** 5.97
- **Average Code Health (LoC-weighted):** 7.13
- **Worst Performer(s):** osafw-app/App_Code/models/Dev/CodeGen.cs (score 2.47)

**Color distribution**
| Color | Files | % Files | LoC | % LoC |
|---|---|---|---|---|
| Green | 1105 | 94.93% | 25255 | 45.87% |
| Yellow | 51 | 4.38% | 20384 | 37.02% |
| Red | 8 | 0.69% | 9416 | 17.10% |

## 4) Refactoring Targets
| Rank | File | Priority Score | Health | Color | Change Count | LoC | Max CC | Dup% | Authors | Coupling |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | osafw-app/App_Code/fw/ParsePage.cs | 6.2451 | 2.70 | Red | 27 | 1133 | 75 | 50.04 | 1 | 133 |
| 2 | osafw-app/App_Code/models/Dev/CodeGen.cs | 5.9208 | 2.47 | Red | 17 | 1256 | 85 | 39.65 | 1 | 99 |
| 3 | osafw-app/App_Code/fw/FwDynamicController.cs | 5.6691 | 4.07 | Red | 55 | 1049 | 51 | 52.62 | 2 | 193 |
| 4 | osafw-app/App_Code/fw/FW.cs | 5.4711 | 4.02 | Red | 38 | 1269 | 41 | 38.06 | 1 | 180 |
| 5 | osafw-app/App_Code/fw/DB.cs | 5.3838 | 3.99 | Red | 28 | 2021 | 21 | 54.87 | 2 | 181 |
| 6 | osafw-app/App_Code/fw/FwController.cs | 4.6820 | 4.75 | Red | 36 | 1005 | 21 | 38.71 | 2 | 184 |
| 7 | osafw-app/wwwroot/assets/js/fw.js | 4.3459 | 4.86 | Red | 27 | 963 | 14 | 34.16 | 2 | 115 |
| 8 | osafw-app/App_Code/fw/FwModel.cs | 3.9159 | 5.29 | Yellow | 24 | 1034 | 17 | 56.77 | 2 | 171 |
| 9 | osafw-app/App_Code/fw/Utils.cs | 3.8630 | 5.66 | Yellow | 33 | 1203 | 15 | 50.54 | 3 | 168 |
| 10 | osafw-tests/App_Code/fw/UtilsTests.cs | 3.7481 | 5.08 | Yellow | 15 | 1186 | 13 | 36.51 | 2 | 99 |

## 5) Hotspots
Hotspot cutoff determined by knee-point heuristic on change-count distribution: **6** changes.
| Rank | File | Change Count | Churn Total | LoC | Hotspot Score |
|---|---|---|---|---|---|
| 1 | osafw-app/App_Code/fw/FwDynamicController.cs | 55 | 2153 | 1049 | 0.956 |
| 2 | osafw-app/App_Code/fw/FW.cs | 38 | 2406 | 1269 | 0.9149 |
| 3 | osafw-app/App_Code/fw/FwController.cs | 36 | 1249 | 1005 | 0.8918 |
| 4 | osafw-app/App_Code/fw/Utils.cs | 33 | 1687 | 1203 | 0.8901 |
| 5 | osafw-app/App_Code/fw/DB.cs | 28 | 2536 | 2021 | 0.8958 |
| 6 | osafw-app/App_Code/fw/ParsePage.cs | 27 | 1492 | 1133 | 0.8555 |
| 7 | osafw-app/wwwroot/assets/css/site.css | 27 | 3348 | 1023 | 0.8492 |
| 8 | osafw-app/wwwroot/assets/js/fw.js | 27 | 725 | 963 | 0.8455 |
| 9 | osafw-app/App_Code/fw/FwVueController.cs | 25 | 882 | 437 | 0.7843 |
| 10 | osafw-app/App_Code/fw/FwModel.cs | 24 | 1052 | 1034 | 0.8314 |
| 11 | osafw-app/appsettings.json | 24 | 73 | 126 | 0.6945 |
| 12 | osafw-app/App_Code/fw/FormUtils.cs | 22 | 530 | 523 | 0.7765 |
| 13 | osafw-app/App_Code/models/Users.cs | 20 | 662 | 607 | 0.7709 |
| 14 | osafw-app/App_Data/template/admin/demosdynamic/config.json | 20 | 1308 | 675 | 0.7772 |
| 15 | osafw-app/App_Code/controllers/DevManage.cs | 18 | 3345 | 471 | 0.7392 |
| 16 | osafw-app/App_Code/fw/FwAdminController.cs | 18 | 214 | 192 | 0.6835 |
| 17 | osafw-app/App_Code/models/Att.cs | 18 | 507 | 470 | 0.7391 |
| 18 | osafw-app/App_Data/template/common/vue/store.js | 18 | 1687 | 835 | 0.7728 |
| 19 | osafw-app/App_Code/controllers/AdminDemos.cs | 17 | 295 | 178 | 0.6703 |
| 20 | osafw-app/App_Code/models/Dev/CodeGen.cs | 17 | 2664 | 1256 | 0.7863 |

## 6) Worst performers & Red code inventory
| Rank | File | Score | LoC | Max CC | Dup% |
|---|---|---|---|---|---|
| 1 | osafw-app/App_Code/models/Dev/CodeGen.cs | 2.47 | 1256 | 85 | 39.65 |
| 2 | osafw-app/App_Code/fw/ParsePage.cs | 2.70 | 1133 | 75 | 50.04 |
| 3 | osafw-app/App_Code/fw/DB.cs | 3.99 | 2021 | 21 | 54.87 |
| 4 | osafw-app/App_Code/fw/FW.cs | 4.02 | 1269 | 41 | 38.06 |
| 5 | osafw-app/App_Code/fw/FwDynamicController.cs | 4.07 | 1049 | 51 | 52.62 |
| 6 | osafw-app/App_Code/fw/FwController.cs | 4.75 | 1005 | 21 | 38.71 |
| 7 | osafw-app/App_Code/models/Dev/EntityBuilder.cs | 4.75 | 720 | 39 | 45.97 |
| 8 | osafw-app/wwwroot/assets/js/fw.js | 4.86 | 963 | 14 | 34.16 |

## 7) Findings by smell category
**Module-level smells**
- Large files (>1000 LoC) in hotspots indicate high change cost.
- High duplication percentage suggests potential for shared utilities.

**Function-level smells**
- Functions with max CC > 10 or NLOC > 100 indicate complex logic needing decomposition.

**Implementation-level smells**
- Hotspots with high churn and low health are priority for refactoring.

## 8) Trends
**Monthly change counts (last 12 months)**
| File | Sparkline | Counts |
|---|---|---|
| osafw-app/App_Code/fw/ParsePage.cs | ▁█▁▁▂▁▁▃▁▁▆▁ | [0, 8, 0, 0, 2, 0, 0, 3, 1, 1, 6, 0] |
| osafw-app/App_Code/models/Dev/CodeGen.cs | ▁▅▁▁▂▂▁▃▂▁█▂ | [0, 3, 0, 0, 1, 1, 0, 2, 1, 0, 5, 1] |
| osafw-app/App_Code/fw/FwDynamicController.cs | ▁▆▁▁▁▂▁▇▁▁█▅ | [1, 9, 0, 0, 1, 3, 0, 10, 1, 0, 11, 7] |
| osafw-app/App_Code/fw/FW.cs | ▁█▁▁▁▁▁▁▂▁█▄ | [1, 9, 0, 0, 1, 0, 0, 1, 2, 1, 9, 5] |
| osafw-app/App_Code/fw/DB.cs | ▁▃▁▁▁▁▁▄▃▁█▁ | [0, 3, 0, 0, 1, 0, 0, 5, 3, 0, 10, 0] |

**Complexity trend proxy (LoC + max CC at month-end)**

*osafw-app/App_Code/fw/ParsePage.cs*
| Month | LoC | Max CC |
|---|---|---|
| 2025-02 | 1248 | 56 |
| 2025-03 | 1244 | 56 |
| 2025-04 | 1244 | 56 |
| 2025-05 | 1244 | 56 |
| 2025-06 | 1251 | 56 |
| 2025-07 | 1251 | 56 |
| 2025-08 | 1251 | 56 |
| 2025-09 | 1411 | 56 |
| 2025-10 | 1409 | 56 |
| 2025-11 | 1409 | 56 |
| 2025-12 | 1372 | 75 |
| 2026-01 | 1372 | 75 |

*osafw-app/App_Code/models/Dev/CodeGen.cs*
| Month | LoC | Max CC |
|---|---|---|
| 2025-02 | 1253 | 63 |
| 2025-03 | 1249 | 62 |
| 2025-04 | 1249 | 62 |
| 2025-05 | 1249 | 62 |
| 2025-06 | 1295 | 66 |
| 2025-07 | 1301 | 66 |
| 2025-08 | 1301 | 66 |
| 2025-09 | 1309 | 67 |
| 2025-10 | 1309 | 67 |
| 2025-11 | 1309 | 67 |
| 2025-12 | 1405 | 82 |
| 2026-01 | 1418 | 85 |

*osafw-app/App_Code/fw/FwDynamicController.cs*
| Month | LoC | Max CC |
|---|---|---|
| 2025-02 | 1054 | 37 |
| 2025-03 | 1063 | 35 |
| 2025-04 | 1063 | 35 |
| 2025-05 | 1063 | 35 |
| 2025-06 | 1063 | 35 |
| 2025-07 | 1081 | 45 |
| 2025-08 | 1081 | 45 |
| 2025-09 | 1225 | 41 |
| 2025-10 | 1237 | 41 |
| 2025-11 | 1237 | 41 |
| 2025-12 | 1253 | 51 |
| 2026-01 | 1329 | 51 |

*osafw-app/App_Code/fw/FW.cs*
| Month | LoC | Max CC |
|---|---|---|
| 2025-02 | 1536 | 36 |
| 2025-03 | 1532 | 36 |
| 2025-04 | 1532 | 36 |
| 2025-05 | 1532 | 36 |
| 2025-06 | 1400 | 36 |
| 2025-07 | 1400 | 36 |
| 2025-08 | 1400 | 36 |
| 2025-09 | 1434 | 36 |
| 2025-10 | 1494 | 38 |
| 2025-11 | 1475 | 38 |
| 2025-12 | 1505 | 41 |
| 2026-01 | 1516 | 41 |

*osafw-app/App_Code/fw/DB.cs*
| Month | LoC | Max CC |
|---|---|---|
| 2025-02 | 1945 | 16 |
| 2025-03 | 2170 | 16 |
| 2025-04 | 2170 | 16 |
| 2025-05 | 2170 | 16 |
| 2025-06 | 2225 | 16 |
| 2025-07 | 2225 | 16 |
| 2025-08 | 2225 | 16 |
| 2025-09 | 2304 | 16 |
| 2025-10 | 2318 | 16 |
| 2025-11 | 2318 | 16 |
| 2025-12 | 2517 | 21 |
| 2026-01 | 2517 | 21 |

## 9) Recommendations
**1. osafw-app/App_Code/fw/ParsePage.cs**
- **Refactor strategy:** Split large methods, extract shared helpers, and add tests around hottest paths.
- **Expected benefit:** Lower change risk and reduce defect introduction in frequently modified code.
- **Risk:** Requires coordination among frequent contributors to avoid merge conflicts.
**2. osafw-app/App_Code/models/Dev/CodeGen.cs**
- **Refactor strategy:** Split large methods, extract shared helpers, and add tests around hottest paths.
- **Expected benefit:** Lower change risk and reduce defect introduction in frequently modified code.
- **Risk:** Requires coordination among frequent contributors to avoid merge conflicts.
**3. osafw-app/App_Code/fw/FwDynamicController.cs**
- **Refactor strategy:** Split large methods, extract shared helpers, and add tests around hottest paths.
- **Expected benefit:** Lower change risk and reduce defect introduction in frequently modified code.
- **Risk:** Requires coordination among frequent contributors to avoid merge conflicts.
**4. osafw-app/App_Code/fw/FW.cs**
- **Refactor strategy:** Split large methods, extract shared helpers, and add tests around hottest paths.
- **Expected benefit:** Lower change risk and reduce defect introduction in frequently modified code.
- **Risk:** Requires coordination among frequent contributors to avoid merge conflicts.
**5. osafw-app/App_Code/fw/DB.cs**
- **Refactor strategy:** Split large methods, extract shared helpers, and add tests around hottest paths.
- **Expected benefit:** Lower change risk and reduce defect introduction in frequently modified code.
- **Risk:** Requires coordination among frequent contributors to avoid merge conflicts.

## 10) Appendix
**Scoring model**
- Start at 10.0, apply penalties for file size, max function size, cyclomatic complexity, average complexity, duplication, and hotspot churn.
- Color bands: Green >= 8.0, Yellow 5.0–7.9, Red < 5.0.

**Ignored paths**
- .git, .next, .nuxt, Pods, bin, build, coverage, dist, node_modules, obj, out, target, vendor
**Ignored patterns**
- *.min.*, *.generated.*, *.Designer.cs

**Tools**
- lizard 1.20.0 (cyclomatic complexity)
- Custom Python scripts for LoC and git analytics

**Limitations**
- Duplication detection is line-based and within-file; cross-file clone detection not performed.
- Complexity trends are sampled at month-end commits and may miss mid-month spikes.

---
**Summary:** Hotspots with low health and high churn should be prioritized; see Refactoring Targets for actionable list.
