---
name: probuilder-connect-edges
description: "Inserts new edges connecting the midpoints of selected edges within faces.
If a face has more than 2 edges to connect, a center vertex is added.
This is useful for creating new edge loops and adding geometry detail.

Examples:
- Connect opposite edges of top face: faceDirection=\"up\"
- Connect specific edges: edges=[[0,1], [2,3]]"
---

# Connect edges in a ProBuilder mesh

Inserts new edges connecting the midpoints of selected edges within faces.
If a face has more than 2 edges to connect, a center vertex is added.
This is useful for creating new edge loops and adding geometry detail.

Examples:
- Connect opposite edges of top face: faceDirection="up"
- Connect specific edges: edges=[[0,1], [2,3]]

## How to Call

### HTTP API (Direct Tool Execution)

Execute this tool directly via the MCP Plugin HTTP API:

```bash
curl -X POST http://localhost:58239/api/tools/probuilder-connect-edges \
  -H "Content-Type: application/json" \
  -d '{
  "gameObjectRef": "string_value",
  "edges": "string_value",
  "faceDirection": "string_value"
}'
```

#### With Authorization (if required)

```bash
curl -X POST http://localhost:58239/api/tools/probuilder-connect-edges \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
  "gameObjectRef": "string_value",
  "edges": "string_value",
  "faceDirection": "string_value"
}'
```

> The token is stored in the file: `UserSettings/AI-Game-Developer-Config.json`
> Using the format: `"token": "YOUR_TOKEN"`

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `gameObjectRef` | `any` | Yes | Reference to the GameObject with a ProBuilderMesh component. |
| `edges` | `any` | No | Array of edge definitions. Each edge is [vertexA, vertexB]. Use ProBuilder_GetMeshInfo to get vertex indices. |
| `faceDirection` | `any` | No | Semantic face selection - connect edges of faces facing this direction. |

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
      "description": "Array of edge definitions. Each edge is [vertexA, vertexB]. Use ProBuilder_GetMeshInfo to get vertex indices."
    },
    "faceDirection": {
      "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Editor.API.FaceDirection",
      "description": "Semantic face selection - connect edges of faces facing this direction."
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
    },
    "com.IvanMurzak.Unity.MCP.Editor.API.FaceDirection": {
      "type": "string",
      "enum": [
        "Up",
        "Down",
        "Left",
        "Right",
        "Forward",
        "Back"
      ]
    }
  },
  "required": [
    "gameObjectRef"
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
      "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BConnectEdgesResponse"
    }
  },
  "$defs": {
    "com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BConnectEdgesResponse": {
      "type": "object",
      "properties": {
        "selectionMethod": {
          "type": "string"
        },
        "edgesConnected": {
          "type": "integer"
        },
        "newFacesCreated": {
          "type": "integer"
        },
        "newEdgesCreated": {
          "type": "integer"
        },
        "faceCountBefore": {
          "type": "integer"
        },
        "faceCountAfter": {
          "type": "integer"
        },
        "facesAdded": {
          "type": "integer"
        },
        "edgeCountBefore": {
          "type": "integer"
        },
        "edgeCountAfter": {
          "type": "integer"
        },
        "edgesAdded": {
          "type": "integer"
        },
        "totalVertexCount": {
          "type": "integer"
        }
      },
      "required": [
        "edgesConnected",
        "newFacesCreated",
        "newEdgesCreated",
        "faceCountBefore",
        "faceCountAfter",
        "facesAdded",
        "edgeCountBefore",
        "edgeCountAfter",
        "edgesAdded",
        "totalVertexCount"
      ]
    }
  },
  "required": [
    "result"
  ]
}
```

