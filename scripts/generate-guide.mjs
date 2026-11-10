import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

// Produce an editable Word document from the repository guide, without packages.
const source = process.argv[2] ? resolve(process.argv[2]) : resolve(import.meta.dirname, '../docs/arquitectura-y-publicacion.md');
const target = process.argv[3] ? resolve(process.argv[3]) : resolve(import.meta.dirname, '../docs/arquitectura-y-publicacion.docx');
const escape = value => value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
const plain = value => value.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '$1 ($2)').replaceAll('**', '').replaceAll('`', '');

function paragraph(text, style = 'Normal') {
    return `<w:p><w:pPr><w:pStyle w:val="${style}"/></w:pPr><w:r><w:t xml:space="preserve">${escape(text)}</w:t></w:r></w:p>`;
}
const lines = readFileSync(source, 'utf8').split(/\r?\n/);
const body = [];
let code = false;

for (let index = 0; index < lines.length; index++) {
    const line = lines[index];

    if (line.startsWith('```')) {
        code = !code;
        continue;
    }

    if (code) {
        body.push(paragraph(line, 'Code'));
        continue;
    }

    if (!line.trim()) continue;

    if (line.startsWith('|')) {
        const rows = [];
        while (index < lines.length && lines[index].startsWith('|')) {
            if (!/^\|[\s:|\-]+\|$/.test(lines[index])) rows.push(lines[index].slice(1, -1).split('|').map(cell => plain(cell.trim())));
            index++;
        }
        index--;
        const columnCount = rows[0]?.length || 1;
        const widths = columnCount === 3 ? [2300, 2200, 5406] : columnCount === 4 ? [1100, 3300, 2600, 2906] : Array(columnCount).fill(Math.floor(9906 / columnCount));
        body.push('<w:tbl><w:tblPr><w:tblW w:w="9906" w:type="dxa"/><w:tblBorders><w:top w:val="single" w:sz="4" w:color="CCD6CC"/><w:left w:val="single" w:sz="4" w:color="CCD6CC"/><w:bottom w:val="single" w:sz="4" w:color="CCD6CC"/><w:right w:val="single" w:sz="4" w:color="CCD6CC"/><w:insideH w:val="single" w:sz="4" w:color="CCD6CC"/><w:insideV w:val="single" w:sz="4" w:color="CCD6CC"/></w:tblBorders><w:tblLayout w:type="fixed"/></w:tblPr><w:tblGrid>' + widths.map(width => `<w:gridCol w:w="${width}"/>`).join('') + '</w:tblGrid>' + rows.map((row, rowIndex) => '<w:tr>' + (rowIndex === 0 ? '<w:trPr><w:tblHeader/></w:trPr>' : '') + row.map((cell, columnIndex) => `<w:tc><w:tcPr><w:tcW w:w="${widths[columnIndex]}" w:type="dxa"/>${rowIndex === 0 ? '<w:shd w:fill="EFF3EC"/>' : ''}</w:tcPr>` + paragraph(cell) + '</w:tc>').join('') + '</w:tr>').join('') + '</w:tbl>');
        continue;
    }
    const heading = line.match(/^(#{1,3})\s+(.*)$/);

    if (heading) body.push(paragraph(plain(heading[2]), heading[1].length === 1 ? 'Title' : `Heading${heading[1].length - 1}`));
    else body.push(paragraph(plain(line.startsWith('- ') ? '• ' + line.slice(2) : line)));
}
const declaration = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>';
const entries = new Map([
    [
        '[Content_Types].xml',
        declaration + '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/><Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/></Types>'
    ],
    [
        '_rels/.rels',
        declaration + '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>'
    ],
    [
        'word/_rels/document.xml.rels',
        declaration + '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>'
    ],
    [
        'word/document.xml',
        declaration + '<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>' + body.join('') + '<w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1000" w:right="1000" w:bottom="1000" w:left="1000"/></w:sectPr></w:body></w:document>'
    ],
    [
        'word/styles.xml',
        declaration + '<w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/><w:pPr><w:spacing w:after="140" w:line="276" w:lineRule="auto"/></w:pPr><w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri"/><w:sz w:val="22"/></w:rPr></w:style><w:style w:type="paragraph" w:styleId="Title"><w:name w:val="Title"/><w:basedOn w:val="Normal"/><w:rPr><w:b/><w:color w:val="17634E"/><w:sz w:val="40"/></w:rPr></w:style><w:style w:type="paragraph" w:styleId="Heading1"><w:name w:val="heading 1"/><w:basedOn w:val="Normal"/><w:pPr><w:keepNext/><w:spacing w:before="260"/><w:outlineLvl w:val="0"/></w:pPr><w:rPr><w:b/><w:color w:val="17634E"/><w:sz w:val="30"/></w:rPr></w:style><w:style w:type="paragraph" w:styleId="Heading2"><w:name w:val="heading 2"/><w:basedOn w:val="Normal"/><w:pPr><w:keepNext/><w:outlineLvl w:val="1"/></w:pPr><w:rPr><w:b/><w:sz w:val="25"/></w:rPr></w:style><w:style w:type="paragraph" w:styleId="Code"><w:name w:val="Code"/><w:basedOn w:val="Normal"/><w:pPr><w:spacing w:after="0"/><w:shd w:fill="EFF3EC"/></w:pPr><w:rPr><w:rFonts w:ascii="Consolas" w:hAnsi="Consolas"/><w:sz w:val="18"/></w:rPr></w:style></w:styles>'
    ],
]);
entries.set(
    '[Content_Types].xml',
    entries.get('[Content_Types].xml').replace(
        '</Types>',
        '<Override PartName="/word/footer1.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml"/></Types>'
    )
);
entries.set(
    'word/_rels/document.xml.rels',
    entries.get('word/_rels/document.xml.rels').replace(
        '</Relationships>',
        '<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer" Target="footer1.xml"/></Relationships>'
    )
);
entries.set(
    'word/document.xml',
    entries.get('word/document.xml').replace(
        '<w:document ',
        '<w:document xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" '
    ).replace(
        '<w:sectPr>',
        '<w:sectPr><w:footerReference w:type="default" r:id="rId2"/>'
    )
);
entries.set(
    'word/footer1.xml',
    declaration + '<w:ftr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:p><w:pPr><w:jc w:val="center"/></w:pPr><w:r><w:t xml:space="preserve">GeometryDash · Página </w:t></w:r><w:fldSimple w:instr="PAGE"><w:r><w:t>1</w:t></w:r></w:fldSimple><w:r><w:t xml:space="preserve"> de </w:t></w:r><w:fldSimple w:instr="NUMPAGES"><w:r><w:t>1</w:t></w:r></w:fldSimple></w:p></w:ftr>'
);
const crcTable = Array.from(
    {
        length: 256
    },
    (_, index) => {
        let value = index;

        for (let bit = 0; bit < 8; bit++) value = value & 1 ? 0xedb88320 ^ (value >>> 1) : value >>> 1;

        return value >>> 0;
    }
);
const crc32 = data => {
    let crc = 0xffffffff;

    for (const byte of data) crc = crcTable[(crc ^ byte) & 255] ^ (crc >>> 8);

    return (crc ^ 0xffffffff) >>> 0;
};
const chunks = [];
const central = [];
let offset = 0;
const now = new Date();
const time = (now.getHours() << 11) | (now.getMinutes() << 5) | (now.getSeconds() >> 1);
const date = ((now.getFullYear() - 1980) << 9) | ((now.getMonth() + 1) << 5) | now.getDate();

for (const [name, text] of entries) {
    const filename = Buffer.from(name);
    const data = Buffer.from(text);
    const checksum = crc32(data);
    const local = Buffer.alloc(30);
    local.writeUInt32LE(0x04034b50);
    local.writeUInt16LE(20, 4);
    local.writeUInt16LE(time, 10);
    local.writeUInt16LE(date, 12);
    local.writeUInt32LE(checksum, 14);
    local.writeUInt32LE(data.length, 18);
    local.writeUInt32LE(data.length, 22);
    local.writeUInt16LE(filename.length, 26);
    const directory = Buffer.alloc(46);
    directory.writeUInt32LE(0x02014b50);
    directory.writeUInt16LE(20, 4);
    directory.writeUInt16LE(20, 6);
    directory.writeUInt16LE(time, 12);
    directory.writeUInt16LE(date, 14);
    directory.writeUInt32LE(checksum, 16);
    directory.writeUInt32LE(data.length, 20);
    directory.writeUInt32LE(data.length, 24);
    directory.writeUInt16LE(filename.length, 28);
    directory.writeUInt32LE(offset, 42);
    chunks.push(local, filename, data);
    central.push(directory, filename);
    offset += local.length + filename.length + data.length;
}
const centralBuffer = Buffer.concat(central);
const end = Buffer.alloc(22);
end.writeUInt32LE(0x06054b50);
end.writeUInt16LE(entries.size, 8);
end.writeUInt16LE(entries.size, 10);
end.writeUInt32LE(centralBuffer.length, 12);
end.writeUInt32LE(offset, 16);
writeFileSync(target, Buffer.concat([...chunks, centralBuffer, end]));
console.log('Documento Word generado: ' + target);
