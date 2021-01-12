# darkFX - A dark theme for DocFX!
A dark template for documentation generated with [DocFX](https://dotnet.github.io/docfx/).
This is an override of the default template so you need to enable both in the `docfx.json`.

![darkFX - Screenshots](./images/darkfx-screenshots.png)

## Install

1. Download the source or the zipped file from the [releases](https://github.com/steffen-wilke/darkfx/releases).
2. Create a `templates` folder in the root of your DocFX directory.
3. Copy the `darkfx` folder to the `templates` folder.
4. Update the `docfx.json` configuration to include the material template:
    ```json
    {
        "template": [
            "default",
            "templates/darkfx"
        ],
    }
    ```

## Acknowledgement
Many thanks to [Oscar Vásquez](https://github.com/ovasquez) from which I borrowed the example pages and repository structure of his [Material Theme for DocFX](https://github.com/ovasquez/docfx-material).