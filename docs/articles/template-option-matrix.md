# Template Option Support Matrix

The machine-readable support contract is `eng/template-option-matrix.json`. CI reads that file to construct the job
matrix, and `eng/Test-TemplateOptionMatrix.ps1` verifies that its option values match `.template.config/template.json`,
that the complete cross product is present without duplicates, and that this table contains every supported combination.

| `authProvider` | `dbProvider` | Authentication | Fallback authorization | Data provider | Migration content |
|:---|:---|:---|:---|:---|:---|
| `cookie` | `sqlite` | Enabled | Authenticated by default | SQLite | Included |
| `cookie` | `sqlserver` | Enabled | Authenticated by default | SQL Server | SQLite history excluded |
| `cookie` | `none` | Enabled | Authenticated by default | Disabled by configuration | Retained reusable source |
| `none` | `sqlite` | Disabled | Public unless another policy is added | SQLite | Included |
| `none` | `sqlserver` | Disabled | Public unless another policy is added | SQL Server | SQLite history excluded |
| `none` | `none` | Disabled | Public unless another policy is added | Disabled by configuration | Retained reusable source |

Each CI entry packs and installs the template, generates a clean application, validates option-specific output, checks
the generated lock files, restores in locked mode, builds in Release configuration, and runs the generated tests.

Universal scaffold validation also checks the generated manifest. Provider-specific assertions are limited to the
authentication settings, fallback-authorization setting, selected data provider, connection-string name, and migration
content represented by the selected matrix entry.

## Unsupported values

`authProvider` and `dbProvider` are template choice parameters. Values absent from this matrix are unsupported and are
rejected by `dotnet new` choice validation. Adding a new choice to `.template.config/template.json` without adding its
complete matrix coverage causes `eng/Test-TemplateOptionMatrix.ps1` to fail.

## Assurance boundary

Matrix validation proves that supported template combinations generate, restore, build, test, and retain their declared
configuration and scaffold invariants. It does not claim that every generated application is penetration-tested,
production-certified, or secure without application-specific review and deployment controls.
