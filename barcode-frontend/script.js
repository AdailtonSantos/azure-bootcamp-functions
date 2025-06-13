const form = document.getElementById('barcodeForm');
const dateInput = document.getElementById('dateInput');
const numberInput = document.getElementById('numberInput');
const barcodeText = document.getElementById('barcodeText');
const barcodeImage = document.getElementById('barcodeImage');

form.addEventListener('submit', async (event) => {
    event.preventDefault();
    console.log('Form submitted');

    const dateValue = dateInput.value;
    const numberValue = numberInput.value;

    const response = await fetch('http://localhost:7133/api/barcode-generate', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify({
            dataVencimento: dateValue,
            valor: numberValue,
        }),
    });

    if (response.ok) {
        const data = await response.json();
        barcodeText.textContent = data.barcode;
        barcodeImage.src = `data:image/png;base64,${data.imageBase64}`;
    } else {
        console.error('Error:', response.statusText);
    }
});