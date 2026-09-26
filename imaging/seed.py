"""Generate synthetic Part 10 files and store through STOW-RS; no patient data."""
import base64
import io
import json
import math
import os
from pathlib import Path
import struct
import urllib.request
import uuid

from pydicom.dataset import FileDataset, FileMetaDataset
from pydicom.uid import ExplicitVRLittleEndian, SecondaryCaptureImageStorage


def uid(label):
    return '2.25.' + str(uuid.uuid5(uuid.NAMESPACE_URL, 'clinicflow:synthetic:v1:' + label).int)


def generate(patient, study, series, instance):
    label = f'{patient}/{study}'
    meta = FileMetaDataset()
    meta.MediaStorageSOPClassUID = SecondaryCaptureImageStorage
    meta.MediaStorageSOPInstanceUID = uid(f'{label}/{series}/{instance}')
    meta.TransferSyntaxUID = ExplicitVRLittleEndian
    meta.ImplementationClassUID = uid('implementation')
    ds = FileDataset(None, {}, file_meta=meta, preamble=b'\0' * 128)
    ds.SOPClassUID = meta.MediaStorageSOPClassUID
    ds.SOPInstanceUID = meta.MediaStorageSOPInstanceUID
    ds.StudyInstanceUID = uid(label)
    ds.SeriesInstanceUID = uid(f'{label}/{series}')
    ds.PatientID = f'CF-IMG-{patient:03}'
    ds.IssuerOfPatientID = 'ClinicFlowDemo'
    ds.PatientName = f'SYNTHETIC^PATIENT{patient}'
    ds.PatientBirthDate = ''
    ds.PatientSex = ''
    ds.StudyDate = '20260101'
    ds.StudyTime = '090000'
    ds.AccessionNumber = f'DEMO-{patient}-{study}'
    ds.StudyID = str(study)
    ds.StudyDescription = f'Synthetic phantom {study} - NOT CLINICAL'
    ds.SeriesDescription = f'Geometric pattern {series}'
    ds.ReferringPhysicianName = ''
    ds.Modality = 'OT'
    ds.ConversionType = 'WSD'
    ds.SeriesNumber = series
    ds.InstanceNumber = instance
    ds.Manufacturer = 'ClinicFlow educational fixture'
    ds.ImageType = ['DERIVED', 'SECONDARY']
    ds.Rows = ds.Columns = 128
    ds.SamplesPerPixel = 1
    ds.PhotometricInterpretation = 'MONOCHROME2'
    ds.BitsAllocated = ds.BitsStored = 16
    ds.HighBit = 15
    ds.PixelRepresentation = 0
    ds.WindowCenter = 2048
    ds.WindowWidth = 4096
    values = []
    for y in range(128):
        for x in range(128):
            r = math.hypot(x - 64, y - 64)
            values.append(int((x + y) * 8 + (1800 if r < 18 + instance * 5 else 0)))
    ds.PixelData = struct.pack('<' + 'H' * len(values), *values)
    stream = io.BytesIO()
    ds.save_as(stream, enforce_file_format=True)
    return ds, stream.getvalue()


def main():
    output = Path('artifacts/dicom-fixtures')
    output.mkdir(parents=True, exist_ok=True)
    base = os.environ.get('Imaging__BaseUrl', 'http://127.0.0.1:8042/').rstrip('/')
    password = os.environ['INTEGRATION_TOKEN']
    auth = base64.b64encode(f'clinicflow:{password}'.encode()).decode()
    manifest = []
    for patient, study in [(1, 1), (1, 2), (2, 1)]:
        for series in (1, 2):
            for instance in (1, 2, 3):
                ds, data = generate(patient, study, series, instance)
                (output / f'{patient}-{study}-{series}-{instance}.dcm').write_bytes(data)
                boundary = 'clinicflow-fixture-boundary'
                body = (f'--{boundary}\r\nContent-Type: application/dicom\r\n\r\n'.encode()
                        + data + f'\r\n--{boundary}--\r\n'.encode())
                request = urllib.request.Request(base + '/dicom-web/studies', data=body, headers={
                    'Authorization': 'Basic ' + auth,
                    'Content-Type': f'multipart/related; type="application/dicom"; boundary={boundary}',
                    'Accept': 'application/dicom+json',
                })
                with urllib.request.urlopen(request, timeout=30) as response:
                    result = json.load(response)
                    if result.get('00081198', {}).get('Value'):
                        raise RuntimeError('STOW-RS reported failed instances')
        manifest.append({'patientId': patient, 'studyInstanceUid': str(ds.StudyInstanceUID),
                         'seriesInstanceUid': str(ds.SeriesInstanceUID),
                         'sopInstanceUid': str(ds.SOPInstanceUID)})
    (output / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print('Stored 18 synthetic instances / 6 series / 3 studies through STOW-RS.')
    print('Stable UIDs make reruns safe; manifest: artifacts/dicom-fixtures/manifest.json')


if __name__ == '__main__':
    main()
