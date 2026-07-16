import { writeFile } from 'node:fs/promises'
import openapiTS, { astToString } from 'openapi-typescript'

const source = process.env.PAWCONNECT_OPENAPI_URL
  ?? 'http://localhost:5180/swagger/v1/swagger.json'
const output = new URL('../src/api/generated/schema.d.ts', import.meta.url)

const nodes = await openapiTS(source)
await writeFile(output, astToString(nodes), 'utf8')
console.log(`Generated ${output.pathname} from ${source}`)
