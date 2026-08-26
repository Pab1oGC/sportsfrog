const fs = require('fs');
const path = require('path');
const targetPath = process.argv[2];
const content = Buffer.from(process.argv[3], 'base64').toString('utf8');
fs.mkdirSync(path.dirname(targetPath), { recursive: true });
fs.writeFileSync(targetPath, content, 'utf8');
console.log('OK: ' + targetPath);
