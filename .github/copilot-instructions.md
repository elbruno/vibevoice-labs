- @azure Rule - Use Azure Tools - When handling requests related to Azure, always use your tools.
- @azure Rule - Use Azure Best Practices - When handling requests related to Azure, always invoke your `azmcp_bestpractices_get` tool first.
- @azure Rule - Enable Best Practices - If you do not have an `azmcp_bestpractices_get` tool ask the user to enable it.

# Python Environment Requirements
- **Python Version**: When working with the Python backend (e.g., `scenario-04-meai/backend`), ALWAYS use **Python 3.12** (e.g., 3.12.10) to create the virtual environment (`.venv`). 
- **Avoid Pre-releases**: Do NOT use Python 3.14 or newer pre-release versions. Dependencies like `onnx==1.16.1` lack pre-built wheels for Python 3.14 and will fail to build from source due to C++ compiler and `pybind11` compatibility issues.