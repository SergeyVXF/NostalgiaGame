---
name: probuilder-bevel
description: "Bevels selected edges of a ProBuilder mesh, creating chamfered corners.
Use ProBuilder_GetMeshInfo to identify edges by their vertex pairs.
Beveling replaces sharp edges with angled faces for a smoother appearance."
---

# Bevel ProBuilder edges

Bevels selected edges of a ProBuilder mesh, creating chamfered corners.
Use ProBuilder_GetMeshInfo to identify edges by their vertex pairs.
Beveling replaces sharp edges with angled faces for a smoother appearance.

## How to Call

### HTTP API (Direct Tool Execution)

Execute this tool directly via the MCP Plugin HTTP API:

```bash
curl -X POST http://localhost:58239/api/tools/probuilder-bevel \
  -H "Content-Type: application/json" \
  -d '{
  "gameObjectRef": "string_value",
  "edges": "string_value",
  "amount": 0
}'
```

#### With Authorization (if required)

```bash
curl -X POST http://localhost:58239/api/tools/probuilder-bevel \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
  "gameObjectRef": "string_value",
  "edges": "string_value",
  "amount": 0
}'
```

> The token is stored in the file: `UserSettings/AI-Game-Developer-Config.json`
> Using the format: `"token": "YOUR_TOKEN"`

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `gameObjectRef` | `any` | Yes | Reference to the GameObject with a ProBuilderMesh component. |
| `edges` | `any` | Yes | Array of edge definitions. Each edge is defined by two vertex indices [vertexA, vertexB]. Example: [[0,1], [2,3]] bevels edges from vertex 0 to 1 and from vertex 2 to 3. |
| `amount` | `number` | No | Bevel amount from 0 (no bevel) to 1 (maximum bevel reaching face center). Recommended values: 0.05 to 0.2. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "gameObjectRef": {
      "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Runtime.Data.GameObjectRef",
      "description": "Reference to the GameObject with a ProBuilderMesh component."
    },
    "edges": {
      "$ref": "#/$defs/System.Int32[][]",
      "description": "Array of edge definitions. Each edge is defined by two vertex indices [vertexA, vertexB]. Example: [[0,1], [2,3]] bevels edges from vertex 0 to 1 and from vertex 2 to 3."
    },
    "amount": {
      "type": "number",
      "description": "Bevel amount from 0 (no bevel) to 1 (maximum bevel reaching face center). Recommended values: 0.05 to 0.2."
    }
  },
  "$defs": {
    "System.Type": {
      "type": "string"
    },
    "com.IvanMurzak.Unity.MCP.Runtime.Data.GameObjectRef": {
      "type": "object",
      "properties": {
        "instanceID": {
          "type": "integer",
          "description": "instanceID of the UnityEngine.Object. If it is \u00270\u0027 and \u0027path\u0027, \u0027name\u0027, \u0027assetPath\u0027 and \u0027assetGuid\u0027 is not provided, empty or null, then it will be used as \u0027null\u0027. Priority: 1 (Recommended)"
        },
        "path": {
          "type": "string",
          "description": "Path of a GameObject in the hierarchy Sample \u0027character/hand/finger/particle\u0027. Priority: 2."
        },
        "name": {
          "type": "string",
          "description": "Name of a GameObject in hierarchy. Priority: 3."
        },
        "assetType": {
          "$ref": "#/$defs/System.Type",
          "description": "Type of the asset."
        },
        "assetPath": {
          "type": "string",
          "description": "Path to the asset within the project. Starts with \u0027Assets/\u0027"
        },
        "assetGuid": {
          "type": "string",
          "description": "Unique identifier for the asset."
        }
      },
      "required": [
        "instanceID"
      ],
      "description": "Find GameObject in opened Prefab or in the active Scene."
    },
    "System.Int32[]": {
      "type": "array",
      "items": {
        "type": "integer"
      }
    },
    "System.Int32[][]": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/System.Int32[]"
      }
    }
  },
  "required": [
    "gameObjectRef",
    "edges"
  ]
}
```

## Output

### Output JSON Schema

```json
{
  "type": "object",
  "properties": {
    "result": {
      "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BBevelResponse"
    }
  },
  "$defs": {
    "com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BBevelResponse": {
      "type": "object",
      "properties": {
        "edgesBeveled": {
          "type": "integer"
        },
        "bevelAmount": {
          "type": "number"
        },
        "newFacesCreated": {
          "type": "integer"
        },
        "totalFaceCount": {
          "type": "integer"
        },
        "totalVertexCount": {
          "type": "integer"
        },
        "totalEdgeCount": {
          "type": "integer"
        }
      },
      "required": [
        "edgesBeveled",
        "bevelAmount",
        "newFacesCreated",
        "totalFaceCount",
        "totalVertexCount",
        "totalEdgeCount"
      ]
    }
  },
  "required": [
    "result"
  ]
}
```

