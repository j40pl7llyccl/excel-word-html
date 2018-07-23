import mammoth
#C:\Users\jordan\BOND_CODE.docx
filename = raw_input("input filename:")
with open(r"C:\Users\jordan\Desktop\FDC_WORD\{}.docx".format(filename), "rb") as docx_file:
    result = mammoth.convert_to_html(docx_file)
    html = result.value # The generated HTML
    messages = result.messages # Any messages, such as warnings during conversion