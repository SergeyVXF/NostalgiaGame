---
name: probuilder-get-mesh-info
description: "Retrieves information about a ProBuilder mesh including faces, vertices, and edges.
Use detail=\"summary\" for a token-efficient overview showing face directions.
Use detail=\"full\" for detailed face-by-face information.

TIP: With semantic face selection (faceDirection parameter) in Extrude/DeleteFaces/SetFaceMaterial,
you often don't need GetMeshInfo at all - just use faceDirection=\"up\" etc. directly."
---

# Get ProBuilder mesh information

Retrieves information about a ProBuilder mesh including faces, vertices, and edges.
Use detail="summary" for a token-efficient overview showing face directions.
Use detail="full" for detailed face-by-face information.

TIP: With semantic face selection (faceDirection parameter) in Extrude/DeleteFaces/SetFaceMaterial,
you often don't need GetMeshInfo at all - just use faceDirection="up" etc. directly.

## How to Call

### HTTP API (Direct Tool Execution)

Execute this tool directly via the MCP Plugin HTTP API:

```bash
curl -X POST http://localhost:58239/api/tools/probuilder-get-mesh-info \
  -H "Content-Type: application/json" \
  -d '{
  "gameObjectRef": "string_value",
  "detail": "string_value",
  "includeVertexPositions": false,
  "includeEdges": false,
  "maxFacesToShow": 0
}'
```

#### With Authorization (if required)

```bash
curl -X POST http://localhost:58239/api/tools/probuilder-get-mesh-info \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
  "gameObjectRef": "string_value",
  "detail": "string_value",
  "includeVertexPositions": false,
  "includeEdges": false,
  "maxFacesToShow": 0
}'
```

> The token is stored in the file: `UserSettings/AI-Game-Developer-Config.json`
> Using the format: `"token": "YOUR_TOKEN"`

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `gameObjectRef` | `any` | Yes | Reference to the GameObject with a ProBuilderMesh component. |
| `detail` | `string` | No | Detail level for output. |
| `includeVertexPositions` | `boolean` | No | If true, includes detailed vertex positions for each face (only with detail='full'). |
| `includeEdges` | `boolean` | No | If true, includes edge information for each face (only with detail='full'). |
| `maxFacesToShow` | `integer` | No | Maximum number of faces to include in detail (only with detail='full'). Use -1 for all faces. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "gameObjectRef": {
      "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Runtime.Data.GameObjectRef",
      "description": "Reference to the GameObject with a ProBuilderMesh component."
    },
    "detail": {
      "type": "string",
      "enum": [
        "Summary",
        "Full"
      ],
      "description": "Detail level for output."
    },
    "includeVertexPositions": {
      "type": "boolean",
      "description": "If true, includes detailed vertex positions for each face (only with detail=\u0027full\u0027)."
    },
    "includeEdges": {
      "type": "boolean",
      "description": "If true, includes edge information for each face (only with detail=\u0027full\u0027)."
    },
    "maxFacesToShow": {
      "type": "integer",
      "description": "Maximum number of faces to include in detail (only with detail=\u0027full\u0027). Use -1 for all faces."
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
      "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BGetMeshInfoResponse"
    }
  },
  "$defs": {
    "com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BBoundsInfo": {
      "type": "object",
      "properties": {
        "center": {
          "type": "string"
        },
        "size": {
          "type": "string"
        },
        "min": {
          "type": "string"
        },
        "max": {
          "type": "string"
        }
      }
    },
    "System.Collections.Generic.List\u003Ccom.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BFaceDirectionInfo\u003E": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BFaceDirectionInfo"
      }
    },
    "com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BFaceDirectionInfo": {
      "type": "object",
      "properties": {
        "direction": {
          "type": "string"
        },
        "faceIndices": {
          "$ref": "#/$defs/System.Int32[]"
        },
        "firstFaceCenter": {
          "type": "string"
        }
      }
    },
    "System.Int32[]": {
      "type": "array",
      "items": {
        "type": "integer"
      }
    },
    "System.Collections.Generic.List\u003Ccom.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BFaceInfo\u003E": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BFaceInfo"
      }
    },
    "com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BFaceInfo": {
      "type": "object",
      "properties": {
        "index": {
          "type": "integer"
        },
        "vertexCount": {
          "type": "integer"
        },
        "triangleCount": {
          "type": "integer"
        },
        "center": {
          "type": "string"
        },
        "vertices": {
          "$ref": "#/$defs/System.Collections.Generic.List\u003Ccom.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BVertexInfo\u003E"
        },
        "edges": {
          "$ref": "#/$defs/System.Collections.Generic.List\u003Ccom.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BEdgeInfo\u003E"
        }
      },
      "required": [
        "index",
        "vertexCount",
        "triangleCount"
      ]
    },
    "System.Collections.Generic.List\u003Ccom.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BVertexInfo\u003E": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BVertexInfo"
      }
    },
    "com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BVertexInfo": {
      "type": "object",
      "properties": {
        "index": {
          "type": "integer"
        },
        "position": {
          "type": "string"
        }
      },
      "required": [
        "index"
      ]
    },
    "System.Collections.Generic.List\u003Ccom.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BEdgeInfo\u003E": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BEdgeInfo"
      }
    },
    "com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BEdgeInfo": {
      "type": "object",
      "properties": {
        "vertexA": {
          "type": "integer"
        },
        "vertexB": {
          "type": "integer"
        },
        "positionA": {
          "type": "string"
        },
        "positionB": {
          "type": "string"
        }
      },
      "required": [
        "vertexA",
        "vertexB"
      ]
    },
    "com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BGetMeshInfoResponse": {
      "type": "object",
      "properties": {
        "gameObjectName": {
          "type": "string"
        },
        "instanceId": {
          "type": "integer"
        },
        "faceCount": {
          "type": "integer"
        },
        "vertexCount": {
          "type": "integer"
        },
        "edgeCount": {
          "type": "integer"
        },
        "triangleCount": {
          "type": "integer"
        },
        "bounds": {
          "$ref": "#/$defs/com.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BBoundsInfo"
        },
        "faceDirections": {
          "$ref": "#/$defs/System.Collections.Generic.List\u003Ccom.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BFaceDirectionInfo\u003E"
        },
        "faces": {
          "$ref": "#/$defs/System.Collections.Generic.List\u003Ccom.IvanMurzak.Unity.MCP.Editor.API.Tool_ProBuilder\u002BFaceInfo\u003E"
        },
        "facesShown": {
          "type": "integer"
        },
        "facesTotal": {
          "type": "integer"
        },
        "uniqueEdgeCount": {
          "type": "integer"
        }
      },
      "required": [
        "instanceId",
        "faceCount",
        "vertexCount",
        "edgeCount",
        "triangleCount"
      ]
    }
  },
  "required": [
    "result"
  ]
}
```

